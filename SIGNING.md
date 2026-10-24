# Code signing policy

Status: no code-signing provider is enrolled and no signed release is available.

The project intends to seek support for trusted Authenticode signing. Eligibility, acceptance and timing are not guaranteed. No endorsement by a signing provider is claimed.

## Scope and process

- Only executables built from this repository's reviewed source are candidates for signing.
- Vendor SDKs, JV-Link components, downloaded data and private application code are excluded.
- A future signed release must identify its source commit, build procedure, version, unsigned artifact hashes and signed artifact hashes.
- Signing approval must be a separate manual maintainer action. Source changes alone must not automatically approve a signature.
- Signatures and certificate chains must be checked with normal Windows trust validation before use. Strong names and self-signed certificates are not substitutes for trusted Authenticode signing.
- Project team accounts must use multi-factor authentication before enrollment. Enrollment is pending; this document does not assert that account configuration has already been verified.

## Roles

The current author, reviewer and prospective release approver is GitHub user [greenuv213-dotcom](https://github.com/greenuv213-dotcom). This is a single-maintainer project; independent review is not claimed. Provider-required roles and approval rules will be documented if enrollment is accepted.

See [PRIVACY.md](PRIVACY.md) for data handling.
