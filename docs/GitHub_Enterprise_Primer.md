# GitHub Enterprise Primer

## For Developers Scaling from Small Teams to Large Global Projects

### Table of Contents
1. [Repository Organization](#repository-organization)
2. [Branching Strategies](#branching-strategies)
3. [Commit Messages](#commit-messages)
4. [Pull Request Excellence](#pull-request-excellence)
5. [Code Review Culture](#code-review-culture)
6. [Issue Management](#issue-management)
7. [Project Boards](#project-boards)
8. [Automation with Actions](#automation-with-actions)
9. [Security and Compliance](#security-and-compliance)
10. [Global Team Workflows](#global-team-workflows)

## Repository Organization

### Monorepo vs Polyrepo for Enterprise

```yaml
Monorepo Structure:
  collaborative-puzzle/
    ├── .github/
    │   ├── workflows/
    │   ├── CODEOWNERS
    │   ├── PULL_REQUEST_TEMPLATE/
    │   └── ISSUE_TEMPLATE/
    ├── services/
    │   ├── puzzle-api/
    │   ├── auth-service/
    │   ├── notification-service/
    │   └── shared-libraries/
    ├── web/
    │   ├── admin-portal/
    │   └── puzzle-app/
    ├── mobile/
    │   ├── ios/
    │   └── android/
    ├── infrastructure/
    │   ├── terraform/
    │   └── kubernetes/
    └── docs/

Polyrepo Structure:
  org/puzzle-api
  org/puzzle-web
  org/puzzle-mobile
  org/puzzle-infrastructure
  org/puzzle-shared
```

### Repository Settings for Large Teams

```yaml
Branch Protection Rules:
  main:
    - Require pull request reviews: 2
    - Dismiss stale reviews: true
    - Require review from CODEOWNERS: true
    - Require status checks: true
    - Require branches up to date: true
    - Include administrators: true
    - Restrict push access: true
    
  develop:
    - Require pull request reviews: 1
    - Require status checks: true
    - No direct pushes allowed

Repository Permissions:
  Teams:
    core-maintainers: Admin
    senior-developers: Write
    developers: Write
    contractors: Read
    qa-team: Read
```

### CODEOWNERS File

```bash
# .github/CODEOWNERS
# Global owners
* @org/core-maintainers

# Frontend
/web/ @org/frontend-team @org/ui-architects
/mobile/ @org/mobile-team
*.css @org/ui-team
*.scss @org/ui-team

# Backend
/services/ @org/backend-team
/services/auth-service/ @org/security-team @org/backend-team
/services/payment-service/ @org/payment-team @org/security-team

# Infrastructure
/infrastructure/ @org/devops-team
/k8s/ @org/platform-team
*.tf @org/infrastructure-team

# Documentation
/docs/ @org/tech-writers @org/core-maintainers
*.md @org/documentation-team

# Security-sensitive files
.env* @org/security-team
**/secrets/ @org/security-team
**/security/ @org/security-team
```

## Branching Strategies

### Git Flow for Enterprise

```bash
# Main branches
main                    # Production-ready code
├── develop            # Integration branch
├── release/2024.1     # Release preparation
├── hotfix/cve-2024    # Emergency fixes

# Feature branches
feature/JIRA-1234-user-authentication
feature/JIRA-5678-payment-integration

# Team-specific patterns
feature/team-alpha/sprint-23/story-123
feature/team-beta/epic-456/subtask-789
```

### Branch Naming Conventions

```yaml
Pattern: {type}/{ticket}-{description}
Examples:
  feature/PROJ-123-add-oauth-support
  bugfix/PROJ-456-fix-memory-leak
  hotfix/PROJ-789-security-patch
  chore/PROJ-012-update-dependencies
  docs/PROJ-345-api-documentation

Types:
  feature: New functionality
  bugfix: Bug fixes
  hotfix: Emergency production fixes
  chore: Maintenance tasks
  docs: Documentation only
  test: Test additions/fixes
  refactor: Code refactoring
  perf: Performance improvements
```

## Commit Messages

### Conventional Commits Standard

```bash
# Format
<type>(<scope>): <subject>

<body>

<footer>

# Examples
feat(auth): implement OAuth2 login flow

- Add Google OAuth provider
- Add Microsoft OAuth provider  
- Create OAuth callback handler
- Update user model with provider info

Closes #123, #456
BREAKING CHANGE: OAuth config now required

fix(api): resolve memory leak in connection pool

The connection pool was not properly releasing connections
after timeout, causing memory to grow unbounded.

- Implement proper connection disposal
- Add connection timeout handler
- Add metrics for pool monitoring

Fixes #789
```

### Commit Types and Scopes

```yaml
Types:
  feat: New feature
  fix: Bug fix
  docs: Documentation changes
  style: Code style changes (formatting, etc)
  refactor: Code changes that neither fix bugs nor add features
  perf: Performance improvements
  test: Test additions or corrections
  build: Build system changes
  ci: CI configuration changes
  chore: Maintenance tasks
  revert: Revert previous commit

Scopes (examples):
  api: API changes
  auth: Authentication
  ui: User interface
  db: Database
  config: Configuration
  deps: Dependencies
  security: Security-related
  i18n: Internationalization
```

### Commit Message Guidelines

```markdown
## DO's
- Use imperative mood ("add" not "added")
- Keep subject line under 50 characters
- Capitalize the subject line
- Don't end subject with period
- Separate subject from body with blank line
- Wrap body at 72 characters
- Explain what and why, not how
- Reference issues and PRs

## DON'Ts
- Don't use generic messages ("fix bug", "update")
- Don't combine unrelated changes
- Don't commit commented-out code
- Don't commit debugging code
- Don't use emoji (unless team convention)

## Good vs Bad Examples

❌ Bad:
- "Fixed stuff"
- "Updated code"
- "bug fix"
- "Adding new feature for users to login with social media"

✅ Good:
- "fix(auth): resolve token expiration edge case"
- "feat(ui): add dark mode toggle to settings"
- "perf(api): optimize database query for user search"
- "docs(readme): add installation instructions for Windows"
```

## Pull Request Excellence

### PR Template

```markdown
## Description
Brief description of what this PR does

## Type of Change
- [ ] 🐛 Bug fix (non-breaking change)
- [ ] ✨ New feature (non-breaking change)
- [ ] 💥 Breaking change
- [ ] 📝 Documentation update
- [ ] ♻️ Refactoring
- [ ] ⚡ Performance improvement

## Related Issues
Closes #123
Relates to #456

## Changes Made
- List of specific changes
- Implementation approach
- Any design decisions

## Testing
- [ ] Unit tests added/updated
- [ ] Integration tests added/updated
- [ ] Manual testing completed
- [ ] Performance testing completed

## Screenshots
If applicable, add screenshots

## Checklist
- [ ] Self-review completed
- [ ] Code follows style guidelines
- [ ] Comments added for complex logic
- [ ] Documentation updated
- [ ] No new warnings
- [ ] Tests pass locally
- [ ] PR title follows conventional commits

## Breaking Changes
List any breaking changes and migration guide

## Dependencies
- [ ] Database migration required
- [ ] Configuration changes needed
- [ ] Infrastructure changes required

## Deployment Notes
Special instructions for deployment

## Reviewers
@team-lead - Required
@domain-expert - For business logic
@security-team - If security implications
```

### PR Best Practices

```yaml
Size Guidelines:
  Ideal: < 400 lines
  Maximum: < 1000 lines
  
  If larger:
    - Split into multiple PRs
    - Create feature branch with sub-PRs
    - Use stacked PRs approach

PR Lifecycle:
  1. Create Draft PR early
  2. Add [WIP] prefix while working
  3. Push commits regularly
  4. Request reviews when ready
  5. Address feedback promptly
  6. Keep PR updated with base branch
  7. Squash or merge based on team convention

Good PR Titles:
  - "feat(api): Add pagination to user endpoint (#123)"
  - "fix(auth): Resolve race condition in token refresh (#456)"
  - "docs(api): Update OpenAPI schema for v2 endpoints (#789)"
  
Bad PR Titles:
  - "Fixed bug"
  - "Updates"
  - "JIRA-123"
  - "Work on feature"
```

### Stacked PRs for Large Features

```bash
# Base feature branch
feature/epic-oauth-integration
  │
  ├── PR #1: feat(auth): add OAuth provider interface
  │
  ├── PR #2: feat(auth): implement Google OAuth
  │
  ├── PR #3: feat(auth): implement Microsoft OAuth
  │
  └── PR #4: feat(ui): add OAuth login buttons

# Each PR builds on previous, reviewed independently
```

## Code Review Culture

### Code Review Guidelines

```markdown
## For Reviewers

### Review Checklist
- [ ] Does the code do what it's supposed to?
- [ ] Is the code readable and maintainable?
- [ ] Are there tests?
- [ ] Is there appropriate error handling?
- [ ] Are there security implications?
- [ ] Is the performance acceptable?
- [ ] Does it follow team conventions?

### Comment Types
🔍 **[Question]**: Seeking clarification
💡 **[Suggestion]**: Non-blocking improvement
⚠️ **[Issue]**: Should be addressed
🚨 **[Blocker]**: Must be fixed before merge
💭 **[Thought]**: General observation
📚 **[Learning]**: Educational comment

### Good Review Comments
✅ "Consider extracting this logic into a helper method for reusability"
✅ "This could throw a NullReferenceException if user is null. Add a null check?"
✅ "Great use of the strategy pattern here! 👍"
✅ "For future reference, we have a utility for this in SharedLib.Helpers"

### Poor Review Comments
❌ "This is wrong"
❌ "I don't like this"
❌ "Why did you do it this way?"
❌ "Rewrite this entire section"
```

### Review Response Etiquette

```markdown
## For Authors

### Responding to Feedback
- Thank reviewers for their time
- Acknowledge all comments
- Explain your reasoning when disagreeing
- Update PR description with changes made

### Response Examples
```
> "Consider using LINQ here for readability"

✅ "Good suggestion! Updated in commit abc123"
✅ "I considered LINQ but went with a loop for performance. Here's my benchmark: ..."
✅ "You're right, this is clearer. Changed to use LINQ."

❌ "No"
❌ "That's not better"
❌ "I like my way"
```

### Handling Disagreements
1. Assume positive intent
2. Focus on the code, not the person
3. Provide data/examples when possible
4. Escalate to tech lead if needed
5. Document decision in ADR if significant
```

## Issue Management

### Issue Templates

```yaml
# .github/ISSUE_TEMPLATE/bug_report.yml
name: Bug Report
description: File a bug report
labels: ["bug", "triage"]
assignees:
  - octocat
body:
  - type: markdown
    attributes:
      value: |
        Thanks for taking the time to fill out this bug report!
  
  - type: textarea
    id: description
    attributes:
      label: Bug Description
      description: Clear and concise description
      placeholder: Tell us what you see!
    validations:
      required: true
      
  - type: dropdown
    id: severity
    attributes:
      label: Severity
      options:
        - Critical (Production Down)
        - High (Major Feature Broken)
        - Medium (Minor Feature Issue)
        - Low (Cosmetic/Edge Case)
    validations:
      required: true
      
  - type: textarea
    id: reproduction
    attributes:
      label: Steps to Reproduce
      description: How can we reproduce this?
      value: |
        1. Go to '...'
        2. Click on '....'
        3. Scroll down to '....'
        4. See error
    validations:
      required: true
```

### Issue Labeling System

```yaml
Priority Labels:
  - P0-critical: Production down
  - P1-high: Major feature broken
  - P2-medium: Should fix soon
  - P3-low: Nice to have

Type Labels:
  - bug: Something isn't working
  - enhancement: New feature request
  - documentation: Documentation improvements
  - question: Further information requested
  - duplicate: This issue already exists
  - wontfix: This will not be worked on

Status Labels:
  - triage: Needs evaluation
  - ready: Ready to work on
  - in-progress: Being worked on
  - blocked: Blocked by dependency
  - review: In code review
  - testing: In QA testing

Component Labels:
  - frontend: UI-related
  - backend: API/Server
  - database: Database-related
  - infrastructure: DevOps/Infra
  - mobile: Mobile apps
  - security: Security-related

Team Labels:
  - team-alpha: Assigned to Alpha
  - team-beta: Assigned to Beta
  - team-platform: Platform team
```

## Project Boards

### Kanban Board Setup

```yaml
Columns:
  Backlog:
    - Automation: Add new issues
    - Limit: None
    
  Ready:
    - Automation: None
    - Limit: 2x team size
    - Description: Refined and ready to start
    
  In Progress:
    - Automation: Move when PR created
    - Limit: 1-2 per developer
    - Description: Active development
    
  In Review:
    - Automation: Move when review requested
    - Limit: Team size
    - Description: PR under review
    
  Testing:
    - Automation: Move when approved
    - Limit: 5
    - Description: QA testing
    
  Done:
    - Automation: Move when PR merged
    - Limit: None
    - Description: Completed this sprint
```

### Sprint Planning with Projects

```markdown
## Sprint Planning Template

### Sprint 23 Goals
- Complete OAuth integration
- Fix critical production bugs
- Improve API performance

### Capacity Planning
| Developer | Capacity | Assigned Points |
|-----------|----------|-----------------|
| Alice     | 13       | 13              |
| Bob       | 13       | 12              |
| Carol     | 8 (vacation) | 8           |

### Risk Items
- [ ] OAuth provider API changes
- [ ] Database migration complexity
- [ ] Third-party service reliability

### Dependencies
- Security team review for OAuth
- DevOps support for deployment
- UX designs for login flow
```

## Automation with Actions

### CI/CD Workflow

```yaml
# .github/workflows/ci-cd.yml
name: CI/CD Pipeline

on:
  pull_request:
    types: [opened, synchronize, reopened]
  push:
    branches: [main, develop]

concurrency:
  group: ${{ github.workflow }}-${{ github.ref }}
  cancel-in-progress: true

jobs:
  validate:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
        with:
          fetch-depth: 0
          
      - name: Validate PR Title
        if: github.event_name == 'pull_request'
        uses: amannn/action-semantic-pull-request@v5
        env:
          GITHUB_TOKEN: ${{ secrets.GITHUB_TOKEN }}
          
      - name: Check Commit Messages
        uses: wagoid/commitlint-github-action@v5
        
  security:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      
      - name: Run Trivy security scan
        uses: aquasecurity/trivy-action@master
        with:
          scan-type: 'fs'
          scan-ref: '.'
          
      - name: Run Snyk security scan
        uses: snyk/actions/dotnet@master
        env:
          SNYK_TOKEN: ${{ secrets.SNYK_TOKEN }}
          
  build-and-test:
    runs-on: ubuntu-latest
    strategy:
      matrix:
        project: [api, web, mobile]
    steps:
      - uses: actions/checkout@v4
      
      - name: Setup .NET
        uses: actions/setup-dotnet@v3
        with:
          dotnet-version: '8.0.x'
          
      - name: Cache dependencies
        uses: actions/cache@v3
        with:
          path: ~/.nuget/packages
          key: ${{ runner.os }}-nuget-${{ hashFiles('**/*.csproj') }}
          
      - name: Build
        run: dotnet build ./src/${{ matrix.project }}
        
      - name: Test
        run: dotnet test ./tests/${{ matrix.project }}.Tests
        
      - name: Upload coverage
        uses: codecov/codecov-action@v3
        with:
          file: ./coverage.xml
          flags: ${{ matrix.project }}
```

### PR Automation

```yaml
# .github/workflows/pr-automation.yml
name: PR Automation

on:
  pull_request:
    types: [opened, edited]

jobs:
  label:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/labeler@v4
        with:
          repo-token: "${{ secrets.GITHUB_TOKEN }}"
          
      - name: Add size labels
        uses: codelytv/pr-size-labeler@v1
        with:
          GITHUB_TOKEN: ${{ secrets.GITHUB_TOKEN }}
          xs_label: 'size/XS'
          xs_max_size: 10
          s_label: 'size/S'
          s_max_size: 100
          m_label: 'size/M'
          m_max_size: 500
          l_label: 'size/L'
          l_max_size: 1000
          xl_label: 'size/XL'
          
  assign:
    runs-on: ubuntu-latest
    steps:
      - name: Auto-assign PR
        uses: kentaro-m/auto-assign-action@v1.2.5
        with:
          configuration-path: '.github/auto-assign.yml'
          
  notify:
    runs-on: ubuntu-latest
    steps:
      - name: Notify Slack
        uses: 8398a7/action-slack@v3
        with:
          status: ${{ job.status }}
          text: 'New PR opened: ${{ github.event.pull_request.title }}'
          webhook_url: ${{ secrets.SLACK_WEBHOOK }}
```

## Security and Compliance

### Security Policies

```markdown
# SECURITY.md

## Security Policy

### Supported Versions
| Version | Supported          |
| ------- | ------------------ |
| 5.1.x   | :white_check_mark: |
| 5.0.x   | :x:                |
| 4.0.x   | :white_check_mark: |
| < 4.0   | :x:                |

### Reporting a Vulnerability
1. **DO NOT** open a public issue
2. Email security@company.com
3. Include:
   - Description of vulnerability
   - Steps to reproduce
   - Potential impact
   - Suggested fix (if any)

### Response Timeline
- Initial response: 24 hours
- Status update: 72 hours
- Fix timeline: Based on severity
```

### Branch Protection for Compliance

```yaml
Compliance Requirements:
  SOC2:
    - No direct commits to main
    - All changes via PR
    - Minimum 2 reviewers
    - All checks must pass
    
  HIPAA:
    - Security scan required
    - Audit log retention
    - Access logging enabled
    
  GDPR:
    - Data classification labels
    - Privacy review for data changes
    - Encryption verification
```

## Global Team Workflows

### Time Zone Aware Practices

```yaml
Async-First Communication:
  PR Reviews:
    - 24-hour SLA for initial review
    - Detailed comments for clarity
    - Video explanations for complex changes
    - Clear "done" criteria
    
  Issue Updates:
    - Daily status in issue comments
    - Clear handoff notes
    - Time zone in signatures
    - Next action clearly stated
    
Meeting-Free Zones:
  - 00:00-08:00 UTC: Asia focus time
  - 08:00-16:00 UTC: Europe focus time  
  - 16:00-00:00 UTC: Americas focus time
```

### Cultural Considerations

```markdown
## Communication Styles

### Direct Feedback Cultures (US, Germany, Netherlands)
```diff
+ "This approach won't scale. Consider using pattern X instead."
+ "The performance impact is unacceptable. Please optimize."
```

### Indirect Feedback Cultures (Japan, India, UK)
```diff
+ "I wonder if we might explore alternative approaches?"
+ "Perhaps we could investigate the performance characteristics?"
```

### Universal Best Practices
- Start with appreciation
- Be specific with examples
- Offer solutions, not just problems
- Use "we" instead of "you"
- Acknowledge cultural holidays
```

### Handoff Documentation

```markdown
## Daily Handoff Template

### Date: 2024-01-15
### From: US Team → APAC Team

#### Completed Today
- ✅ Merged PR #123 (OAuth implementation)
- ✅ Fixed critical bug in payment service
- ✅ Updated documentation for API v2

#### In Progress
- 🔄 PR #456 - Needs security review
  - Blocker: Waiting for security team
  - Next: Address comments once received
  
- 🔄 Issue #789 - Database optimization
  - Status: Query analysis complete
  - Next: Implement index changes

#### Needs Attention
- 🚨 Production alert at 23:45 UTC
  - Temporary fix applied
  - Root cause analysis needed
  
#### Questions for Your Team
1. Can you verify the fix works in APAC region?
2. Do we need translations for new OAuth screens?

#### Notes
- Jenkins build #2345 is flaky, rerun if fails
- Customer X reported issue, high priority
```

## GitHub Enterprise Features

### Advanced Security

```yaml
Dependabot:
  version-updates:
    - package-ecosystem: "nuget"
      directory: "/"
      schedule:
        interval: "weekly"
      reviewers:
        - "security-team"
        
  security-updates:
    - package-ecosystem: "npm"
      directory: "/web"
      schedule:
        interval: "daily"
        
Code Scanning:
  - CodeQL analysis
  - Secret scanning
  - Dependency review
  - Security advisories
```

### Insights and Analytics

```yaml
Metrics to Track:
  Team Performance:
    - PR merge time
    - Review turnaround
    - Deployment frequency
    - Failed deployments
    
  Code Quality:
    - Code coverage trends
    - Technical debt
    - Security vulnerabilities
    - Performance metrics
    
  Collaboration:
    - PR participation
    - Issue resolution time
    - Cross-team contributions
    - Documentation updates
```

## Best Practices Summary

### The 10 Commandments of Enterprise GitHub

1. **Write Clear Commit Messages**: Future you will thank present you
2. **Keep PRs Small**: Easier to review, faster to merge
3. **Review Promptly**: Respect your colleagues' time
4. **Document Decisions**: ADRs for architectural choices
5. **Automate Everything**: If you do it twice, automate it
6. **Secure by Default**: Security is everyone's responsibility
7. **Test Before Merge**: Broken builds block everyone
8. **Communicate Asynchronously**: Not everyone is in your timezone
9. **Be Kind in Reviews**: There's a human on the other side
10. **Learn Continuously**: GitHub features evolve, stay updated

### Quick Reference Card

```bash
# Daily Workflow
git checkout develop
git pull origin develop
git checkout -b feature/JIRA-123-description
# ... make changes ...
git add -p  # Stage chunks interactively
git commit  # Write meaningful message
git push -u origin feature/JIRA-123-description
# Create PR via GitHub CLI
gh pr create --title "feat(component): description" \
             --body "$(cat .github/pull_request_template.md)" \
             --reviewer @team-lead \
             --assignee @me \
             --label "enhancement" \
             --project "Sprint 23"

# Review Workflow
gh pr checkout 123
# ... test changes locally ...
gh pr review --approve --body "LGTM! Great work on the error handling."

# Issue Workflow
gh issue create --title "Bug: Description" \
                --body "$(cat .github/issue_template.md)" \
                --label "bug,P2-medium" \
                --assignee @me

# Quick Searches
gh pr list --search "is:open review-requested:@me"
gh issue list --search "is:open assignee:@me label:P0-critical"
```