using Google.OrTools.Sat;
using SmartTimetable.Application.Solving;
using SmartTimetable.Domain.Enums;

namespace SmartTimetable.Solver;

/// <summary>
/// Google OR-Tools CP-SAT implementation of <see cref="ITimetableSolver"/>.
///
/// Decision model (per requirement r = a class studying a subject n periods/week):
///   teach[r,t]    : requirement r is taught by teacher t (exactly one chosen)
///   place[r,d,p]  : a lesson of r sits on day d, period p (exactly n placed)
///   y[r,t,d,p]    : = teach[r,t] AND place[r,d,p] (teacher occupancy)
///
/// Hard constraints: one teacher per requirement, exact weekly counts, per-day
/// spread cap, one subject per class-slot, one class per teacher-slot, teacher
/// availability windows, teacher max workload, lab-room capacity per slot, and
/// combined-class co-scheduling. Soft goals (last period, afternoon load, teacher
/// preferences, minimum workload) are penalties; score = 10000 - penalty.
/// One solution is produced per requested strategy.
/// </summary>
public sealed class CpSatTimetableSolver : ITimetableSolver
{
    public IReadOnlyList<SolverSolution> Solve(SolverInput input, SolverOptions options, CancellationToken ct = default)
    {
        var strategies = options.Strategies is { Count: > 0 }
            ? options.Strategies
            : new List<GenerationStrategy> { GenerationStrategy.Balanced };

        var results = new List<SolverSolution>();

        // ----- Shared derived data (identical across strategies) -----
        var subjectById = input.Subjects.ToDictionary(s => s.Id);
        var classById = input.Classes.ToDictionary(c => c.Id);
        var teacherById = input.Teachers.ToDictionary(t => t.Id);

        var eligibleBySubject = new Dictionary<int, List<int>>();
        foreach (var e in input.Eligibilities)
        {
            if (!eligibleBySubject.TryGetValue(e.SubjectId, out var list))
                eligibleBySubject[e.SubjectId] = list = new List<int>();
            if (!list.Contains(e.TeacherId))
                list.Add(e.TeacherId);
        }

        // Eligible teacher set per requirement (respecting a pinned teacher).
        var eligByReq = new List<List<int>>(input.Requirements.Count);
        foreach (var r in input.Requirements)
        {
            if (r.PinnedTeacherId is int pid && teacherById.ContainsKey(pid))
                eligByReq.Add(new List<int> { pid });
            else
                eligByReq.Add(eligibleBySubject.TryGetValue(r.SubjectId, out var list) ? list : new List<int>());
        }

        // Pre-flight: a requirement with no eligible teacher makes the whole model
        // infeasible, so report it clearly instead of returning a blank failure.
        for (int i = 0; i < input.Requirements.Count; i++)
        {
            if (eligByReq[i].Count == 0)
            {
                var r = input.Requirements[i];
                string cls = classById.TryGetValue(r.ClassId, out var c) ? c.Name : $"Class {r.ClassId}";
                string sub = subjectById.TryGetValue(r.SubjectId, out var s) ? s.Name : $"Subject {r.SubjectId}";
                string msg = $"No teacher is qualified to teach {sub} for {cls}. Map a teacher to this subject.";
                foreach (var strat in strategies)
                    results.Add(new SolverSolution { Strategy = strat, IsFeasible = false, Score = 0, StatusText = msg });
                return results;
            }
        }

        var allowedSlots = new HashSet<(int, int, int)>();
        foreach (var a in input.AllowedSlots)
            allowedSlots.Add((a.TeacherId, a.Day, a.PeriodId));

        int seedOffset = 0;
        foreach (var strategy in strategies)
        {
            ct.ThrowIfCancellationRequested();

            // Custom uses admin-supplied weights when provided; everything else uses
            // its built-in profile.
            var profile = strategy == GenerationStrategy.Custom && options.CustomWeights is not null
                ? StrategyProfile.FromWeights(options.CustomWeights)
                : StrategyProfile.For(strategy);

            results.Add(SolveOne(
                input, strategy, profile,
                subjectById, eligByReq, allowedSlots,
                options.MaxSecondsPerStrategy, options.RandomSeed + seedOffset));
            seedOffset++;
        }

        return results;
    }

    private static SolverSolution SolveOne(
        SolverInput input,
        GenerationStrategy strategy,
        StrategyProfile profile,
        Dictionary<int, SolverSubject> subjectById,
        List<List<int>> eligByReq,
        HashSet<(int, int, int)> allowedSlots,
        double maxSeconds,
        int seed)
    {
        var model = new CpModel();
        var reqs = input.Requirements;
        var days = input.Days;
        var periods = input.Periods;

        // Lessons may only be placed in teaching periods; break slots are never
        // scheduled. Classification flags (last/before-break/afternoon) are still
        // derived from the full ordered list so "afternoon" etc. stay meaningful.
        var teachingPeriods = periods.Where(p => !p.IsBreak).OrderBy(p => p.Order).ToList();

        int labRoomCount = input.Rooms.Count(r => r.IsLab);

        // Period classification helpers.
        var firstTwoIds = teachingPeriods.Take(2).Select(p => p.Id).ToHashSet();
        bool hasBreak = periods.Any(p => p.IsBeforeBreak);
        int beforeBreakOrder = hasBreak ? periods.Where(p => p.IsBeforeBreak).Max(p => p.Order) : int.MaxValue;
        bool IsAfternoon(SolverPeriod p) => hasBreak && p.Order > beforeBreakOrder;

        // Decision variables.
        var teach = new Dictionary<(int, int), BoolVar>();
        var place = new Dictionary<(int, int, int), BoolVar>();
        var y = new Dictionary<(int, int, int, int), BoolVar>();

        // Aggregation buckets for constraints.
        var classSlot = new Dictionary<(int, int, int), List<IntVar>>();     // class no-overlap
        var teacherSlot = new Dictionary<(int, int, int), List<IntVar>>();   // teacher no-overlap
        var teacherLoad = new Dictionary<int, List<IntVar>>();               // teacher workload
        var labSlot = new Dictionary<(int, int), List<IntVar>>();            // lab-room capacity

        // Penalty terms for the objective.
        var penVars = new List<IntVar>();
        var penW = new List<long>();
        void AddPenalty(IntVar v, long w) { if (w > 0) { penVars.Add(v); penW.Add(w); } }

        for (int ri = 0; ri < reqs.Count; ri++)
        {
            var req = reqs[ri];
            var elig = eligByReq[ri];
            bool subjectIsLab = subjectById.TryGetValue(req.SubjectId, out var subj) && subj.IsLab;

            // teach[r,t]; exactly one teacher.
            var teachVars = new List<IntVar>(elig.Count);
            foreach (var t in elig)
            {
                var tv = model.NewBoolVar($"teach_r{ri}_t{t}");
                teach[(ri, t)] = tv;
                teachVars.Add(tv);
            }
            model.Add(LinearExpr.Sum(teachVars.ToArray()) == 1);

            // place[r,d,p]; exactly n placed, capped per day for spread.
            var placeVars = new List<IntVar>(days.Count * teachingPeriods.Count);
            int perDayCap = (int)Math.Ceiling(req.PeriodsPerWeek / (double)Math.Max(1, days.Count));

            // Preferred periods this demand's teacher asked for (soft). Null = no preference.
            var preferredSet = req.PreferencePriority > 0 && req.PreferredPeriodIds.Count > 0
                ? new HashSet<int>(req.PreferredPeriodIds)
                : null;
            long periodPrefWeight = (long)req.PreferencePriority * profile.PeriodPref;

            foreach (var d in days)
            {
                var perDay = new List<IntVar>(teachingPeriods.Count);
                foreach (var p in teachingPeriods)
                {
                    var pv = model.NewBoolVar($"place_r{ri}_d{d}_p{p.Id}");
                    place[(ri, d, p.Id)] = pv;
                    placeVars.Add(pv);
                    perDay.Add(pv);

                    // class occupancy
                    Bucket(classSlot, (req.ClassId, d, p.Id)).Add(pv);

                    // lab-room usage
                    if (subjectIsLab)
                        Bucket(labSlot, (d, p.Id)).Add(pv);

                    // student-comfort penalties
                    if (p.IsLast)
                    {
                        AddPenalty(pv, profile.LastPeriod);
                        if (subjectIsLab) AddPenalty(pv, profile.LabOffPeak);
                    }
                    if (IsAfternoon(p))
                        AddPenalty(pv, profile.Afternoon);

                    // Per-demand preferred-period pull: penalise placing this lecture
                    // in any period the assigned teacher did NOT ask for. Soft — the
                    // solver may still break it to keep the whole timetable feasible.
                    if (preferredSet is not null && !preferredSet.Contains(p.Id))
                        AddPenalty(pv, periodPrefWeight);
                }
                model.Add(LinearExpr.Sum(perDay.ToArray()) <= perDayCap);
            }
            model.Add(LinearExpr.Sum(placeVars.ToArray()) == req.PeriodsPerWeek);

            // y[r,t,d,p] = teach AND place; also enforces availability + occupancy.
            foreach (var t in elig)
            {
                bool restricted = input.Teachers.First(x => x.Id == t).HasAvailabilityRestriction;
                var tv = teach[(ri, t)];
                foreach (var d in days)
                {
                    foreach (var p in teachingPeriods)
                    {
                        var pv = place[(ri, d, p.Id)];
                        var yv = model.NewBoolVar($"y_r{ri}_t{t}_d{d}_p{p.Id}");
                        y[(ri, t, d, p.Id)] = yv;

                        model.Add(yv <= tv);
                        model.Add(yv <= pv);
                        model.Add(yv >= tv + pv - 1);

                        // Availability: if this teacher can't work here, y must be 0,
                        // which (via y >= teach + place - 1) forbids assigning t and placing here together.
                        if (restricted && !allowedSlots.Contains((t, d, p.Id)))
                            model.Add(yv == 0);

                        Bucket(teacherSlot, (t, d, p.Id)).Add(yv);
                        Bucket(teacherLoad, t).Add(yv);
                    }
                }
            }
        }

        // One subject per class per slot.
        foreach (var bucket in classSlot.Values)
            if (bucket.Count > 1)
                model.Add(LinearExpr.Sum(bucket.ToArray()) <= 1);

        // One class per teacher per slot.
        foreach (var bucket in teacherSlot.Values)
            if (bucket.Count > 1)
                model.Add(LinearExpr.Sum(bucket.ToArray()) <= 1);

        // Lab-room capacity per slot.
        if (labRoomCount > 0)
            foreach (var bucket in labSlot.Values)
                model.Add(LinearExpr.Sum(bucket.ToArray()) <= labRoomCount);

        // Teacher workload: hard maximum, soft minimum.
        foreach (var (teacherId, loadVars) in teacherLoad)
        {
            if (loadVars.Count == 0) continue;
            var t = input.Teachers.First(x => x.Id == teacherId);

            var load = model.NewIntVar(0, loadVars.Count, $"load_t{teacherId}");
            model.Add(load == LinearExpr.Sum(loadVars.ToArray()));

            if (t.MaxPeriods >= 0 && t.MaxPeriods < loadVars.Count)
                model.Add(load <= t.MaxPeriods);

            if (t.MinPeriods > 0 && profile.UnderMin > 0)
            {
                var under = model.NewIntVar(0, t.MinPeriods, $"under_t{teacherId}");
                model.Add(under >= t.MinPeriods - load);
                AddPenalty(under, profile.UnderMin);
            }
        }

        // Teacher preferences (soft), scaled by the strategy's TeacherPref multiplier.
        if (profile.TeacherPref > 0)
        {
            foreach (var pref in input.Preferences)
            {
                long w = (long)pref.Weight * profile.TeacherPref;
                if (w <= 0) continue;

                foreach (var ((ri, t, d, pid), yv) in y)
                {
                    if (t != pref.TeacherId) continue;
                    var period = periods.First(p => p.Id == pid);

                    bool hit = pref.Kind switch
                    {
                        PreferenceKind.AvoidLastPeriod => period.IsLast,
                        PreferenceKind.PreferMorning => IsAfternoon(period),
                        PreferenceKind.PreferBeforeBreak => IsAfternoon(period),
                        PreferenceKind.OnlyFirstPeriods => !firstTwoIds.Contains(pid),
                        PreferenceKind.AvoidDay => pref.Day is int pd && pd == d,
                        _ => false
                    };
                    if (hit) AddPenalty(yv, w);
                }
            }
        }

        // Combined classes: co-schedule member sections of the same subject.
        foreach (var combo in input.Combined)
        {
            var memberReqIdx = new List<int>();
            for (int ri = 0; ri < reqs.Count; ri++)
                if (reqs[ri].SubjectId == combo.SubjectId && combo.ClassIds.Contains(reqs[ri].ClassId))
                    memberReqIdx.Add(ri);

            for (int k = 1; k < memberReqIdx.Count; k++)
            {
                int a = memberReqIdx[0], b = memberReqIdx[k];
                foreach (var d in days)
                    foreach (var p in teachingPeriods)
                        model.Add(place[(a, d, p.Id)] == place[(b, d, p.Id)]);
            }
        }

        // Subject pairing: softly reward slots where a lesson of each paired subject
        // runs together, aligning parallel electives across programs. Encoded as a
        // negative penalty (a reward), so it can lower the objective but never makes
        // the model infeasible.
        if (profile.Pairing > 0 && input.Pairings.Count > 0)
        {
            var reqIdxBySubject = new Dictionary<int, List<int>>();
            for (int ri = 0; ri < reqs.Count; ri++)
            {
                if (!reqIdxBySubject.TryGetValue(reqs[ri].SubjectId, out var list))
                    reqIdxBySubject[reqs[ri].SubjectId] = list = new List<int>();
                list.Add(ri);
            }

            long pairReward = profile.Pairing;
            foreach (var pair in input.Pairings)
            {
                if (!reqIdxBySubject.TryGetValue(pair.SubjectAId, out var aReqs) || aReqs.Count == 0) continue;
                if (!reqIdxBySubject.TryGetValue(pair.SubjectBId, out var bReqs) || bReqs.Count == 0) continue;

                foreach (var d in days)
                {
                    foreach (var p in teachingPeriods)
                    {
                        var aPlaces = new List<IntVar>(aReqs.Count);
                        foreach (var ri in aReqs) aPlaces.Add(place[(ri, d, p.Id)]);
                        var bPlaces = new List<IntVar>(bReqs.Count);
                        foreach (var ri in bReqs) bPlaces.Add(place[(ri, d, p.Id)]);

                        // both = 1 only if at least one A-lesson AND one B-lesson sit here.
                        var both = model.NewBoolVar($"pair_a{pair.SubjectAId}_b{pair.SubjectBId}_d{d}_p{p.Id}");
                        model.Add(both <= LinearExpr.Sum(aPlaces.ToArray()));
                        model.Add(both <= LinearExpr.Sum(bPlaces.ToArray()));

                        // Negative weight => the solver gains by aligning them.
                        penVars.Add(both);
                        penW.Add(-pairReward);
                    }
                }
            }
        }

        // Objective.
        if (penVars.Count > 0)
            model.Minimize(LinearExpr.WeightedSum(penVars.ToArray(), penW.ToArray()));

        // Solve.
        var solver = new CpSolver
        {
            StringParameters =
                $"max_time_in_seconds:{maxSeconds.ToString(System.Globalization.CultureInfo.InvariantCulture)};" +
                $"random_seed:{seed};num_search_workers:8"
        };

        CpSolverStatus status = solver.Solve(model);
        bool feasible = status is CpSolverStatus.Optimal or CpSolverStatus.Feasible;

        var solution = new SolverSolution
        {
            Strategy = strategy,
            IsFeasible = feasible,
            StatusText = status.ToString()
        };

        if (!feasible)
        {
            solution.Score = 0;
            return solution;
        }

        long penalty = penVars.Count > 0 ? (long)Math.Round(solver.ObjectiveValue) : 0;
        solution.Score = (int)Math.Clamp(10000 - penalty, 0, 10000);

        // Extract assignments.
        for (int ri = 0; ri < reqs.Count; ri++)
        {
            var req = reqs[ri];

            int chosenTeacher = -1;
            foreach (var t in eligByReq[ri])
            {
                if (solver.Value(teach[(ri, t)]) != 0) { chosenTeacher = t; break; }
            }
            if (chosenTeacher < 0) continue;

            foreach (var d in days)
            {
                foreach (var p in teachingPeriods)
                {
                    if (solver.Value(place[(ri, d, p.Id)]) != 0)
                    {
                        solution.Assignments.Add(new SolverAssignment
                        {
                            ClassId = req.ClassId,
                            SubjectId = req.SubjectId,
                            TeacherId = chosenTeacher,
                            RoomId = null, // filled later by RoomAssigner
                            Day = d,
                            PeriodId = p.Id
                        });
                    }
                }
            }
        }

        return solution;
    }

    private static List<IntVar> Bucket<TKey>(Dictionary<TKey, List<IntVar>> map, TKey key) where TKey : notnull
    {
        if (!map.TryGetValue(key, out var list))
            map[key] = list = new List<IntVar>();
        return list;
    }
}
