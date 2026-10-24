# Privacy and data handling

This bridge does not implement analytics, telemetry, advertising, an update service or a project-operated remote server.

`--probe` reads Windows COM registration information locally without starting COM or requesting network access. Network-enabled operations require the explicit `--allow-network` argument and call the separately installed JV-Link component. JV-Link and its provider may access their services under their own terms and privacy policies; the bridge does not control that implementation.

Raw racing records, local observation timestamps and diagnostic results are written to the invoking process's standard output/error. The calling application decides whether and where to store them. Some JV-Link operations may use local provider-managed caches. Do not publish output, account information, license keys or private logs in issues or commits. No dataset is included in this repository.

GitHub hosts this project's source and issue tracker under GitHub's own privacy policy. Report non-sensitive build problems through repository issues; do not include credentials or customer data.
