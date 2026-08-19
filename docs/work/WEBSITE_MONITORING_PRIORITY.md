# Website Monitoring priority

The product owner requested Website Monitoring on 2026-08-19. The feature is captured as a complete planned subsystem, but the repository's active production MVP remains the higher-priority dependency `#162 -> #116 -> #111`.

Until that P0 dependency is closed or the product owner explicitly changes priority:

- keep Website Monitoring isolated from the selected RC.61 production acceptance chain;
- do not infer or record any P0 external PASS from Website Monitoring CI;
- do not enable outbound website probes or SMTP delivery in the current production package;
- development may proceed on an isolated feature branch with feature-default-disabled behavior and normal validation.
