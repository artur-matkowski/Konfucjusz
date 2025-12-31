# Feature: hCaptcha Registration Safeguard

## Goal
Enhance registration security by integrating hCaptcha into the registration flow, verifying captcha tokens server-side, and aligning configuration with existing environment-variable based secrets.

## High-Level Design
- **Configuration:**
  - `Captcha:Provider` ("hcaptcha").
  - `Captcha:hCaptcha:SiteKey` and `Captcha:hCaptcha:Secret` bound from environment.
- **Service:**
  - `ICaptchaVerifier` interface with method:
    - `Task<bool> VerifyAsync(string token, string remoteIp, CancellationToken ct = default);`
  - Implementation: `HcaptchaVerifier` calling `https://hcaptcha.com/siteverify`.
- **UI Integration:**
  - Registration Razor component renders hCaptcha widget using site key from config.
  - On form submit, the hCaptcha response token is posted and verified before creating user.

## Status
- In progress.
