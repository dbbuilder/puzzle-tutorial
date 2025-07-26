#!/bin/bash

# Generate HTML from Markdown files for GitHub Pages

echo "Generating HTML documentation from Markdown files..."

# Create a simple HTML template function
create_html() {
    local md_file=$1
    local html_file="${md_file%.md}.html"
    local title=$(basename "$md_file" .md | sed 's/_/ /g')
    
    cat > "$html_file" << EOF
<!DOCTYPE html>
<html lang="en">
<head>
    <meta charset="UTF-8">
    <meta name="viewport" content="width=device-width, initial-scale=1.0">
    <title>$title - Collaborative Puzzle Platform</title>
    <link href="https://cdn.jsdelivr.net/npm/bootstrap@5.3.0/dist/css/bootstrap.min.css" rel="stylesheet">
    <link href="https://cdnjs.cloudflare.com/ajax/libs/prism/1.29.0/themes/prism-tomorrow.min.css" rel="stylesheet">
    <style>
        body { font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif; }
        .navbar { background: linear-gradient(135deg, #667eea 0%, #764ba2 100%); }
        .content { max-width: 900px; margin: 0 auto; padding: 40px 20px; }
        pre { background: #2d2d2d; color: #f8f8f2; padding: 15px; border-radius: 8px; overflow-x: auto; }
        code { background: #f3f4f6; padding: 2px 6px; border-radius: 4px; }
        pre code { background: none; padding: 0; }
        h1, h2, h3 { margin-top: 30px; margin-bottom: 20px; }
        table { width: 100%; margin: 20px 0; }
        table th { background: #f3f4f6; }
    </style>
</head>
<body>
    <nav class="navbar navbar-dark">
        <div class="container">
            <a class="navbar-brand" href="index.html">📚 Collaborative Puzzle Docs</a>
            <a href="https://github.com/dbbuilder/puzzle-tutorial" class="btn btn-outline-light btn-sm">GitHub</a>
        </div>
    </nav>
    
    <div class="content">
        <a href="index.html" class="btn btn-sm btn-outline-secondary mb-3">← Back to Index</a>
        <div id="markdown-content">
            <!-- Markdown content will be inserted here -->
            <div class="text-center py-5">
                <div class="spinner-border" role="status">
                    <span class="visually-hidden">Loading...</span>
                </div>
            </div>
        </div>
    </div>

    <script src="https://cdn.jsdelivr.net/npm/marked/marked.min.js"></script>
    <script src="https://cdnjs.cloudflare.com/ajax/libs/prism/1.29.0/prism.min.js"></script>
    <script src="https://cdnjs.cloudflare.com/ajax/libs/prism/1.29.0/components/prism-csharp.min.js"></script>
    <script src="https://cdnjs.cloudflare.com/ajax/libs/prism/1.29.0/components/prism-json.min.js"></script>
    <script src="https://cdnjs.cloudflare.com/ajax/libs/prism/1.29.0/components/prism-yaml.min.js"></script>
    <script src="https://cdnjs.cloudflare.com/ajax/libs/prism/1.29.0/components/prism-bash.min.js"></script>
    <script>
        // Load the markdown file
        fetch('$md_file')
            .then(response => response.text())
            .then(markdown => {
                document.getElementById('markdown-content').innerHTML = marked.parse(markdown);
                Prism.highlightAll();
            })
            .catch(error => {
                document.getElementById('markdown-content').innerHTML = '<p class="text-danger">Error loading documentation.</p>';
            });
    </script>
</body>
</html>
EOF
}

# Generate HTML for all markdown files in docs directory
for md_file in docs/*.md; do
    if [ -f "$md_file" ]; then
        echo "Converting $md_file..."
        create_html "$md_file"
    fi
done

echo "HTML generation complete!"