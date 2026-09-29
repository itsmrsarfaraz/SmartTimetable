# SmartTimetable AI — Vendor Guide

*For you, the vendor (Crafting Colons) — not for the college. This explains how to
build the app, how to turn on licensing, and how to issue a license key when a
college asks for one.*

Keep this file private. It talks about your signing key and the internal tools; the
college never sees any of it. The college-facing manual is `docs/USER_GUIDE.md`.

---

## 0. The short version

There are three build scripts, but in day-to-day use you only ever touch **one or
two of them**, plus a licensing helper:

| You want to… | Run this |
|---|---|
| Make the app you hand to a college (single .exe) | `publish.bat` |
| Make a proper installer (Start-Menu shortcuts + uninstaller) | `build-installer.bat` |
| Just check the whole solution still compiles and tests pass | `build.bat` |
| Create your signing key / issue a license key | `license.bat` |

`build-installer.bat` already runs `publish.bat` for you, so if you want the
installer you can run just that one. `build.bat` is only a health check — it does
**not** produce anything you give to a customer.

The licensing life-cycle, end to end:

```
ONE TIME (you):   license.bat keygen  ->  paste public key into VendorPublicKey.cs  ->  rebuild
PER COLLEGE:      college runs the .exe  ->  it shows an ACTIVATION CODE
                  college sends you the code
                  you:  license.bat issue --request <code> --customer "<name>"
                  you send back the LICENSE KEY
                  college pastes the key, clicks Activate  ->  unlocked for life on that PC
```

---

## 1. The three build scripts, explained

**`build.bat` — the pre-flight check.**
Restores NuGet packages, compiles the whole solution in Release, and runs the tests.
It produces `.dll` files under each project's `bin\` folder — *not* a shippable
program. Use it when you have changed code and just want to confirm everything still
builds and passes. You can skip it entirely and go straight to `publish.bat`; publish
compiles too.

**`publish.bat` — the app you ship.**
Produces `publish\SmartTimetable.exe`. "Self-contained"
means the .NET runtime and the Google OR-Tools native libraries are bundled inside
it, so the college's PC needs nothing pre-installed — they just double-click it. One
caveat: PDF export needs a small `LatoFont\` folder that the build writes *next to*
the exe, so if you hand over the bare program, send the **whole `publish\` folder**
(zipped), not the .exe on its own. It has no installer and no uninstaller; it just
runs.

**`build-installer.bat` — the polished handover (optional).**
Wraps the published exe into `installer-output\SmartTimetable-Setup.exe` using Inno
Setup. The installer creates Start-Menu (and optional desktop) shortcuts, sets the
publisher to *Crafting Colons*, and registers a proper Windows uninstaller whose entry
in *Apps & features* shows your `TimetableDelete.ico`. It runs `publish.bat` first if
`publish\SmartTimetable.exe` is missing, so this one script can do the whole job.
Requires **Inno Setup 6** installed (https://jrsoftware.org/isdl.php) and the two icon
files in place (`src\SmartTimetable.Desktop\Assets\timetable.ico` and
`installer\TimetableDelete.ico`).

**So which do I run?** To test on your own machine, `publish.bat` is enough. To hand
a college a clean, professional install, run `build-installer.bat` and give them
`SmartTimetable-Setup.exe`.

---

## 2. One-time setup: turn on licensing

Do this **once, ever**. Until you do it, the app runs **unlocked in developer mode** —
it skips the activation screen completely and anyone can use it. That is fine while
you are testing, but you must complete this section before giving the app to a paying
college, or your licensing does nothing.

**Step 1 — create your signing key pair.**

```
license.bat keygen
```

You will be asked for a passphrase (minimum 12 characters). This creates two files in
the repo root:

- `vendor_private.pem` — your **secret** signing key, encrypted with that passphrase.
- `vendor_public.txt` — the matching public key (safe to embed in the app).

The tool also prints a line that looks like `public const string SpkiBase64 = "…";`.

> **Back up `vendor_private.pem` and the passphrase somewhere offline** (a USB stick in
> a drawer, a password manager). If you ever lose either one, you can never issue a
> license again for any build that carries this public key — you would have to generate
> a brand-new key and re-ship the app to everyone. `keygen` deliberately refuses to run
> if key files already exist, so it can't overwrite them by accident.

**Step 2 — embed the public key in the app.**

Open `src\SmartTimetable.Licensing\VendorPublicKey.cs` and paste the base64 string
from `keygen` between the quotes:

```csharp
public const string SpkiBase64 = "MFkwEwYHKoZIzj0CAQY...your key...";
```

**Step 3 — rebuild.**

```
publish.bat        (or build-installer.bat)
```

From now on the built app is locked: on a machine with no valid license it shows the
activation screen instead of letting you in. Only your `vendor_private.pem` can sign a
key that this build will accept, because the app only trusts that one public key.

*Never commit `vendor_private.pem` to GitHub.* Only `VendorPublicKey.cs` (the public
half) belongs in the repo.

---

## 3. Giving the app to a college

Hand them **one** of:

- `installer-output\SmartTimetable-Setup.exe` — the installer (**recommended**; it
  bundles the exe and its support files together into one file), or
- the **whole** `publish\` folder, zipped — the bare program. The single-file build
  still writes a small `LatoFont\` folder next to `SmartTimetable.exe` (used by PDF
  export), so send the folder, not just the `.exe` on its own.

That is all they get. They never receive `license.bat`, the LicenseGenerator tool, or
your private key.

When they first open it, because a public key is now embedded and they have no license
yet, they see the **Activation screen**:

- a read-only **"Your activation code"** box — a code unique to that specific computer
  (it is derived from the PC's hardware, so it is different on every machine), and
- a **"License key"** box where they will later paste what you send them.

They copy the activation code and send it to you (email, WhatsApp, whatever).

---

## 4. Issuing a license key (the per-college loop)

When a college sends you their activation code, on **your** machine:

**(Optional) inspect the code first:**

```
license.bat decode --request THEIR-CODE
```

This confirms the code is valid and shows how many hardware identifiers the PC
exposed. It doesn't issue anything.

**Issue the license:**

```
license.bat issue --request THEIR-CODE --customer "Green Valley College"
```

You'll be prompted for your key passphrase. The tool then:

- saves a license file like `SmartTimetable-Green-Valley-College-1a2b3c4d.lic`
  to your **Downloads folder** (just like CraftingPOS drops its key there), so it's
  easy to find and attach to an email,
- prints a **single-line license key** you can just paste into an email, and
- appends a row to `issued_licenses.csv` (next to the `.lic`) so you have a record of
  everything you issued.

Useful options for `issue`:

- `--out "<folder>"` — save the `.lic` somewhere other than Downloads.
- `--expires 2027-12-31` — make it a time-limited license. **Omit it for a lifetime
  license** (the normal case).
- `--customer "…"` — the name shown inside the app after activation ("Activated for …").
- `--edition Standard` — a label stored in the license; leave as default unless you
  sell tiers.
- `--min-matches N` — how many hardware identifiers must still match for the license to
  keep working. The default is chosen automatically and is fine; raising it is stricter
  (a bigger hardware change would break activation), lowering it is more forgiving.

Send the college the single-line key (or the `.lic` file — either works).

**(Optional) double-check a key before sending:**

```
license.bat verify --license SmartTimetable-Green-Valley-College-1a2b3c4d.lic
```

This shows the customer name, edition, issue date and expiry, and confirms the
signature is good.

---

## 5. What the college does to activate

They paste the key into the **License key** box on the activation screen and click
**Activate**. The app checks three things: the signature is really from you, the key
was issued for *this* computer, and it hasn't expired. If all pass, it saves the key
and unlocks — permanently and offline. On later launches it goes straight to the login
screen; they never see the activation screen again.

The saved license lives at `%LOCALAPPDATA%\SmartTimetable\license.lic` on their PC. If
they reinstall the app, their activation (and their timetable database) stay in place,
so they don't need a new key.

---

## 6. Why this is secure (and its limits)

- The app only ever contains your **public** key. It can *verify* a license but can
  never *create* one. Only your `vendor_private.pem` can sign a key, and that never
  leaves your machine — so a college cannot mint its own key, and no one can forge one.
- A key is **locked to the machine** that produced the activation code. Copying an
  activated install to a different PC will fail the machine check, and the app will ask
  for a new request code. That is the intended anti-piracy behaviour.
- Because the machine check tolerates *some* change (`--min-matches`), everyday hardware
  tweaks won't lock a legitimate college out, but a wholly different computer will.
- **Limitation to be aware of:** if you ship a build where `VendorPublicKey.SpkiBase64`
  is still empty, the app runs fully unlocked with no activation gate. Always confirm
  the key is embedded (Section 2) before a real handover. A quick way to be sure: on a
  fresh machine the built app should show the activation screen on first launch.

---

## 7. Quick command reference

```
license.bat keygen                                         # once, ever — make your signing key
license.bat decode --request <code>                        # inspect a college's activation code
license.bat issue  --request <code> --customer "<name>"    # issue a lifetime key
license.bat issue  --request <code> --customer "<name>" --expires 2027-12-31
license.bat verify --license <file.lic>                    # check a key you issued

publish.bat            # build the single .exe to ship
build-installer.bat    # build the Setup.exe installer (runs publish first)
build.bat              # compile + test everything (health check only)
```

The passphrase can also be supplied without a prompt by setting the environment
variable `STT_KEY_PASSPHRASE` before running `license.bat` (handy for scripting, but
don't hard-code it anywhere public).

---

*SmartTimetable AI — a product by Crafting Colons.*
