# Windows native unlock feasibility — v0.4

## Goal
Retain USB removal -> LockWorkStation() and investigate real Windows account unlock using supported, hardware-backed authentication. No fake lock screens or password-injection workarounds.

## Confirm account/device configuration first
Run `whoami` in Windows Terminal to see the account context.
Open **Settings > Accounts > Your info** to determine local account, personal Microsoft account, or work/school account.
Open **Settings > Accounts > Access work or school** to check organization connection.
Run `dsregcmd /status` and inspect AzureAdJoined, DomainJoined, and WorkplaceJoined. Do not publish user identity or tokens from the output.
Open **Settings > Accounts > Sign-in options** to review Windows Hello methods.
Determine whether the USB is an ordinary flash drive or a hardware FIDO2/security key.

## Supported routes
- Entra joined / hybrid joined, configured FIDO2 policy: Windows' supported security key sign-in (often requires a security key PIN or verification), where prerequisites are met.
- Local or consumer Microsoft account + plain USB flash drive: do not promise unattended native unlock. A file token copied from USB is not secure account authentication.
- A custom Credential Provider is a Windows sign-in UI/credential serialization component, not a bypass. It must present an authenticator whose credentials are accepted by LSA / account provider. Do not store Windows passwords for automatic replay.
- Windows Hello Companion Device Framework is deprecated since Windows 10 version 2004; do not base a new implementation on it.

## Security engineering requirements
1. Keep all built-in Windows credential providers enabled and an independently tested password/PIN recovery method.
2. Never silently cache Windows passwords, store credentials on the USB as plaintext, or inject keystrokes into the logon screen.
3. Prefer hardware-backed challenge/response and vendor-supported authentication.
4. Develop provider experiments only in a disposable Windows VM with snapshot recovery and a separate administrator account.
5. No registry installation of any experimental Credential Provider on the daily-use PC.
6. Never interpret simple device insertion or possession of a clonable token as sufficient to impersonate Windows credentials.
7. Avoid changing the working v0.3 locking behavior until the unlock path is confirmed.

## Primary technical references
- https://learn.microsoft.com/en-us/windows/win32/secauthn/credential-providers-in-windows
- https://learn.microsoft.com/en-us/entra/identity/authentication/howto-authentication-passwordless-security-key-windows
- https://learn.microsoft.com/en-us/windows-hardware/design/device-experiences/windows-hello-companion-device-framework

## Next decision
Choose authentication path based on account type and whether a dedicated FIDO2 security key is available. There is currently **no native USB auto-unlock implementation** in this repository.

## Decision for personal Microsoft account + ordinary USB flash drive (2026-10-31)

**Supported approach chosen:** keep USB Guard's native `LockWorkStation()` trigger. Unlock through Windows' built-in Windows Hello PIN, fingerprint, face recognition, or account password. A normal USB mass-storage device does **not** provide a supported unattended Windows unlock method for a personal Microsoft account.

v0.4 adds a **Windows Hello settings** button that opens `ms-settings:signinoptions`. This is a settings shortcut, **not** an auto-unlock implementation. Configure Windows Hello directly in Windows and test it before relying on the app.

### Sign-in setup
1. Open USB Guard -> **Windows Hello settings**, or Windows Settings -> Accounts -> Sign-in options.
2. Set up **Windows Hello PIN** (and fingerprint / face if compatible hardware exists).
3. Use Win+L to lock Windows and verify Windows Hello signs you back in.
4. With your paired USB inserted, start monitoring in USB Guard. Removing it will call Windows native lock after the configured delay. Reinserting it does not replace Windows sign-in.
5. Once signed in, rearm monitoring manually; native lock stops the monitoring session intentionally.

### Not supported or promised
- USB-only automatic unlock of the real Windows lock screen using a clonable flash-drive key file.
- Password replay, registry bypasses, simulated keystrokes at Winlogon, or disabling other credential providers.
- Assuming security key sign-in for work/school Microsoft Entra accounts also applies automatically to a personal Microsoft account's Windows desktop.

For a materially different authentication experience, first choose supported hardware/security key authentication and verify the exact Windows account/device enrollment scenario with Microsoft documentation. Keep a recovery sign-in available at all times.
