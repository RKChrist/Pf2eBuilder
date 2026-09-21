@echo off
rem Both halves of the app, one double-click.
rem
rem This is run.cmd under the name people look for. run.cmd is the one that does the work:
rem it stops anything already running, generates the seed data if it is missing, starts the API
rem on 5092 and the client on 5173, waits for the API to answer /health, and opens the browser.
rem
rem Running the API project on its own does the same thing now: in Development it starts the
rem client itself unless something is already answering on the client's port. See
rem src/Pf2e.Api/Hosting/ClientProcess.cs and Hosting:StartClient in appsettings.json.

call "%~dp0run.cmd" %*
