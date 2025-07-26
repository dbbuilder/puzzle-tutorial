@echo off
echo Starting Markdown Documentation Viewer Server...
echo.
echo Installing dependencies...
call npm install
echo.
echo Starting server...
echo Server will be available at http://localhost:3000
echo Press Ctrl+C to stop the server
echo.
node markdown-server.js