# SmartTimetable AI - Licensing

Goal: sell the app to colleges; one purchase = one computer, lifetime, works offline.

## Flow

```
College PC                              You (vendor)
----------                              ------------
Install app, open "Activate"
App shows REQUEST CODE  ------------->  LicenseGenerator issue --request <code> --customer "<College>"
(AEPUU-JRBC4-35THZ-...)                 -> SmartTimetable-<College>-<id>.lic  (+ one-line key)
Paste key / pick .lic   <-------------  send by email / WhatsApp
App verifies offline, stores license
App works forever on THIS PC only
```

## One-time setup (your machine, never the customer's)

1. `SmartTimetable.LicenseGenerator keygen --out D:\vault\stt-keys`
   creates `vendor_private.pem` (passphrase-encrypted) and `vendor_public.txt`.
2. Back up `vendor_private.pem` + passphrase offline. Lose them = you can no longer issue licenses
   for any build that contains that public key.
3. Paste the public key line into `src/SmartTimetable.Licensing/VendorPublicKey.cs`, rebuild.
4. Never commit or ship the generator or the private key (`.gitignore` already blocks `*private*.pem`).

## Everyday commands

```
LicenseGenerator decode --request <code>          # sanity-check what the customer sent
LicenseGenerator issue  --request <code> --customer "Name"                 # lifetime
LicenseGenerator issue  --request <code> --customer "Name" --expires 2027-08-31   # trial / yearly
LicenseGenerator verify --license file.lic
```
Every issued license is appended to `issued_licenses.csv` next to the output (customer, id, date, request code).
Keep that file - it is your customer register and your reissue history.

## How machine-locking works

The request code holds truncated HMAC hashes of up to 5 identifiers: Windows MachineGuid, SMBIOS UUID,
BIOS serial, motherboard serial, first non-USB disk serial. Placeholder junk ("To Be Filled By O.E.M.",
all-F UUIDs) is discarded. A license stores those hashes and a minimum-match count
(3 when 4-5 identifiers exist, 2 when 3 exist, all when only 2 exist).

* Windows reinstall (MachineGuid changes) -> still valid.
* New disk -> still valid. New disk AND reinstall -> still valid (3 of 5).
* Copied to another PC -> rejected, and copying `license.lic` alone does nothing.
* Motherboard replacement / very cheap PCs with only 2 identifiers -> customer needs a new license
  (ask for a fresh request code; issue again; note it in your register).

## Honest limits

A .NET desktop app can be decompiled and patched by a determined person; no offline scheme prevents that.
This design stops the realistic threat - a college copying the installer or license to more PCs -
and makes forging a license impossible without your private key. To raise the bar before selling:
obfuscate the release build, code-sign the .exe, and call the license check from several places
(startup, before Generate, before Export), not only one dialog.
An attacker can still swap the embedded public key inside a patched exe; that is the same "patch the exe"
threat and is only slowed down by obfuscation.
