const express = require('express');
const fs = require('fs').promises;
const path = require('path');
const marked = require('marked');

const app = express();
const PORT = 3000;
const BASE_PATH = 'D:\\dev2\\puzzletutorial';

// Middleware
app.use(express.static(path.join(__dirname)));
app.use(express.json());

// CORS headers for local development
app.use((req, res, next) => {
    res.header('Access-Control-Allow-Origin', '*');
    res.header('Access-Control-Allow-Headers', 'Content-Type');
    next();
});

// API endpoint to get file structure
app.get('/api/files', async (req, res) => {
    try {
        const fileStructure = {
            'Root': {},
            'docs': {}
        };

        // Read root directory markdown files
        const rootFiles = await fs.readdir(BASE_PATH);
        for (const file of rootFiles) {
            if (file.endsWith('.md')) {
                const fullPath = path.join(BASE_PATH, file);
                const stats = await fs.stat(fullPath);
                if (stats.isFile()) {
                    fileStructure.Root[file] = fullPath;
                }
            }
        }

        // Read docs directory markdown files
        const docsPath = path.join(BASE_PATH, 'docs');
        try {
            const docsFiles = await fs.readdir(docsPath);
            for (const file of docsFiles) {
                if (file.endsWith('.md')) {
                    const fullPath = path.join(docsPath, file);
                    const stats = await fs.stat(fullPath);
                    if (stats.isFile()) {
                        fileStructure.docs[file] = fullPath;
                    }
                }
            }
        } catch (err) {
            console.log('No docs directory found');
        }

        res.json(fileStructure);
    } catch (error) {
        console.error('Error reading file structure:', error);
        res.status(500).json({ error: 'Failed to read file structure' });
    }
});

// API endpoint to get file content
app.get('/api/file', async (req, res) => {
    try {
        const filepath = req.query.path;
        
        // Security check - ensure the path is within BASE_PATH
        const normalizedPath = path.normalize(filepath);
        if (!normalizedPath.startsWith(BASE_PATH)) {
            return res.status(403).json({ error: 'Access denied' });
        }

        const content = await fs.readFile(filepath, 'utf8');
        res.json({ content });
    } catch (error) {
        console.error('Error reading file:', error);
        res.status(500).json({ error: 'Failed to read file' });
    }
});

// Serve the HTML file
app.get('/', (req, res) => {
    res.sendFile(path.join(__dirname, 'markdown-viewer-server.html'));
});

// Start server
app.listen(PORT, () => {
    console.log(`Markdown viewer server running at http://localhost:${PORT}`);
    console.log(`Base path: ${BASE_PATH}`);
});
