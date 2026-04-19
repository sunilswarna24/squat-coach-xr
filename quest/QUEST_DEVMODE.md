# Turning on Developer Mode (Meta Quest 3)

Needed once per Quest before you can `adb install` anything.

## What you need

- The Quest 3 (powered on, paired to your Meta account).
- The **Meta Horizon** app on a phone logged into the **same Meta account**.
- A Meta developer organization (free, 2 minutes — you make one the first
  time you try to toggle dev mode).

## Steps

1. On a laptop or phone browser, go to
   https://developer.oculus.com/manage/organizations/create and create an
   organization. Any name is fine.
2. Accept the developer agreement. This is why Developer Mode couldn't
   toggle before — Meta gates it behind being an "organization member".
3. Open the **Meta Horizon** app on your phone.
4. Tap **Menu** (bottom right) → **Devices** → pick your Quest 3 →
   **Headset Settings** → **Developer Mode** → toggle **ON**.
5. **Reboot the Quest** (hold power, choose Restart). This is what
   actually activates developer mode; skipping the reboot is the #1 cause
   of "dev mode is on but adb still rejects me".

## Verify from your PC

With the Quest USB-C connected:

```bash
adb devices
```

- If you see `unauthorized`, put the headset on, tap **Always allow from
  this computer**, then tap **Allow** on the USB debugging prompt.
- If you see nothing, try a different USB-C cable (many charging-only
  cables don't carry data).

When you see the Quest listed as `device` (not `unauthorized`), you're
cleared to sideload.

## Optional: wireless adb

Easier for iteration during demo day:

```bash
# One-time (wired)
adb tcpip 5555

# Find the Quest IP (Quest's Wi-Fi settings or router)
adb connect <quest-ip>:5555

# Unplug USB, everything still works over Wi-Fi.
```
