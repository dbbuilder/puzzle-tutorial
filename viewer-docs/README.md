# README.md

## Markdown Documentation Viewer

An interactive documentation viewer for the Collaborative Puzzle Platform project. This tool provides a clean, organized way to browse and read all markdown documentation files with syntax highlighting and search capabilities.

### Features

- **Tree Navigation**: Organized file structure with collapsible folders
- **Live Search**: Real-time file name filtering
- **Syntax Highlighting**: Beautiful code block rendering
- **Responsive Design**: Works on desktop and mobile devices
- **Caching**: Fast loading of previously viewed files
- **Professional UI**: Clean, modern interface with Tailwind CSS

### Quick Start

#### Option 1: Using the Node.js Server (Recommended)

1. **Install Node.js** if not already installed (https://nodejs.org/)

2. **Install dependencies**:
   ```bash
   npm install
   ```

3. **Start the server**:
   ```bash
   npm start
   ```
   Or use the provided batch file:
   ```bash
   start-viewer.bat
   ```

4. **Open your browser** to http://localhost:3000

#### Option 2: Static HTML File

1. Open `markdown-viewer.html` directly in your browser
2. Note: File content will need to be manually added to the JavaScript

### File Structure

```
puzzletutorial/
├── markdown-viewer.html          # Static HTML viewer
├── markdown-viewer-server.html   # Server-based viewer
├── markdown-server.js           # Express server
├── package.json                 # Node dependencies
├── start-viewer.bat            # Windows startup script
└── viewer-docs/                # Viewer documentation
    ├── README.md
    ├── REQUIREMENTS.md
    ├── TODO.md
    └── FUTURE.md
```

### Usage Guide

#### Navigation
- Click on any file in the tree to view its content
- Click folder names to expand/collapse
- Click "Documentation Overview" to return to the summary

#### Search
- Type in the search box to filter files by name
- Folders automatically expand to show matches
- Clear the search to show all files again

#### Reading
- Markdown is automatically formatted
- Code blocks include syntax highlighting
- Tables, lists, and links are properly styled
- Breadcrumb shows current file location

### Technical Details

#### Dependencies
- **Express**: Web server framework
- **Marked**: Markdown parsing
- **Highlight.js**: Syntax highlighting
- **Tailwind CSS**: Utility-first styling

#### API Endpoints
- `GET /api/files` - Returns file structure
- `GET /api/file?path=...` - Returns file content
- `GET /` - Serves the HTML interface

### Configuration

The server reads files from `D:\dev2\puzzletutorial\` by default. To change this:

1. Edit `markdown-server.js`
2. Update the `BASE_PATH` constant
3. Restart the server

### Browser Support

- Chrome/Edge 90+
- Firefox 88+
- Safari 14+
- Mobile browsers (responsive design)

### Troubleshooting

#### Server won't start
- Ensure Node.js is installed: `node --version`
- Check if port 3000 is available
- Run `npm install` to ensure dependencies are installed

#### Files not showing
- Verify the BASE_PATH in server configuration
- Check that markdown files exist in the directory
- Ensure file permissions allow reading

#### Styling issues
- Clear browser cache
- Ensure internet connection for CDN resources
- Check browser console for errors

### Contributing

To enhance the viewer:

1. Fork the repository
2. Create a feature branch
3. Make your improvements
4. Test thoroughly
5. Submit a pull request

### License

MIT License - See the main project license file for details.