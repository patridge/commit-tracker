## commit-tracker

A Cake script for monitoring changes made to paths within a Git repo, ideal for monitoring via VSTS continuous ~~integration~~monitoring tasks.

### Script tasks

#### Default

If you don't specify a script target, it will run the **Debug-InfoDump** task. This task will output your currently set variables, from environment variables and/or overridden by the current command line call. As well, it will help information for the various variable parameters and the available tasks.

This task doesn't change anything, so it is safe to run it to make sure arguments are configured properly.
