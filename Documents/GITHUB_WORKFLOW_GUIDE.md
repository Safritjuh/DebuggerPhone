# 🚀 SIP Phone - GitHub Issues Workflow Guide

## 📋 **Overview**
This document provides the complete workflow for using GitHub Issues as the single source of truth for bug tracking and feature management in the SIP Phone project.

---

## 🎯 **Quick Start Commands**

### **View Current Issues**
```powershell
# List all open issues
gh issue list --state open

# View issues by priority
gh issue list --label "priority:critical" --state open
gh issue list --label "priority:high" --state open
gh issue list --label "priority:medium" --state open

# View issues by category
gh issue list --label "sip" --state open
gh issue list --label "audio" --state open
gh issue list --label "ui" --state open
```

### **Start Working on an Issue**
```powershell
# Create branch and switch to it (recommended)
gh issue develop 15 --checkout

# OR manually create branch
git checkout -b bug/BUG-015
```

### **View Issue Details**
```powershell
# View issue in terminal
gh issue view 15

# View issue in browser
gh issue view 15 --web
```

---

## 🏷️ **Label System**

### **Priority Labels**
- `priority:critical` - System crashes, core functionality broken
- `priority:high` - Major features not working, significant user impact
- `priority:medium` - Minor bugs, UI improvements, performance issues
- `priority:low` - Nice-to-have features, cosmetic improvements

### **Category Labels**
- `sip` - SIP protocol, registration, call handling
- `audio` - RTP, codecs, microphone, speakers
- `ui` - User interface, windows, controls
- `network` - Network connectivity, socket handling
- `rtp` - Real-time transport protocol issues
- `configuration` - Settings, preferences, configuration files
- `registration` - SIP account registration and authentication

### **Type Labels**
- `bug` - Something is broken or not working correctly
- `enhancement` - Improvement to existing functionality
- `feature` - New functionality or capability
- `documentation` - Updates to documentation or comments

---

## 📝 **Creating Issues**

### **Bug Report Template**
```powershell
gh issue create \
  --title "BUG-XXX: Descriptive Bug Title" \
  --label "bug,priority:high,sip" \
  --body "
## 🐛 Bug Description
Brief description of the bug

## 🔄 Steps to Reproduce
1. Step one
2. Step two
3. Expected vs Actual behavior

## 🖥️ Environment
- Windows Version: Windows 11
- .NET Version: 8.0
- SIP Server: [Server details]

## 📋 Additional Information
Any other relevant details
"
```

### **Feature Request Template**
```powershell
gh issue create \
  --title "FEATURE: New Feature Name" \
  --label "enhancement,priority:medium" \
  --body "
## 🚀 Feature Description
Description of the requested feature

## 💡 Use Case
Why this feature is needed

## 📋 Acceptance Criteria
- [ ] Requirement 1
- [ ] Requirement 2
- [ ] Requirement 3
"
```

---

## 🔄 **Development Workflow**

### **1. Select Issue to Work On**
```powershell
# Find high-priority issues
gh issue list --label "priority:critical" --state open
gh issue list --label "priority:high" --state open
```

### **2. Create Development Branch**
```powershell
# Recommended: Auto-create branch linked to issue
gh issue develop 15 --checkout

# Branch will be named: 15-descriptive-issue-title
```

### **3. Make Changes and Commit**
```powershell
# Standard development process
git add .
git commit -m "Fix BUG-015: Theme Switcher Not Functional

- Updated ThemeSwitcher component to handle theme changes
- Fixed state management issue in MainWindow  
- Added proper event handlers for theme selection

Fixes #15"
```

### **4. Push and Create Pull Request**
```powershell
# Push changes
git push origin 15-descriptive-issue-title

# Create pull request (will auto-link to issue)
gh pr create \
  --title "Fix BUG-015: Theme Switcher Not Functional" \
  --body "
## 🔧 Changes Made
- Updated ThemeSwitcher component
- Fixed state management
- Added event handlers

## ✅ Testing
- [x] Manual testing completed
- [x] No build warnings
- [x] Feature works as expected

Fixes #15
"
```

### **5. Review and Merge**
- **NEVER merge directly to main**
- Request review from team members
- Issue will automatically close when PR is merged

---

## 📊 **Issue Management**

### **Adding Comments**
```powershell
# Progress update
gh issue comment 15 --body "Working on this issue. Found the root cause in ThemeSwitcher.cs"

# Solution documentation  
gh issue comment 15 --body "Fixed by updating the theme change event handler. Pull request created."
```

### **Closing Issues**
```powershell
# Close with comment (manual close)
gh issue close 15 --comment "Completed - theme switcher now working correctly"

# Auto-close via PR (recommended)
# Issues close automatically when PR with "Fixes #15" is merged
```

### **Reopening Issues**
```powershell
# If issue needs to be reopened
gh issue reopen 15 --comment "Issue has reoccurred, needs additional investigation"
```

---

## 🎯 **Best Practices**

### **Do's ✅**
- Always use GitHub Issues for bug/feature tracking
- Link branches to issues with `gh issue develop X --checkout`
- Include `Fixes #X` in PR descriptions
- Apply appropriate priority and category labels
- Add detailed reproduction steps for bugs
- Document solutions in issue comments
- Request code review before merging
- Keep issue titles descriptive and consistent

### **Don'ts ❌**
- Don't use local files for bug tracking
- Don't merge directly to main branch
- Don't forget to link PRs to issues
- Don't create issues without proper labels
- Don't close issues without documenting the solution

---

## 🔍 **Useful Search Patterns**

### **Find Issues by Status**
```powershell
# Critical issues that need immediate attention
gh issue list --label "priority:critical" --state open

# Recently closed issues (for reference)
gh issue list --state closed --limit 10

# Issues assigned to you
gh issue list --assignee @me --state open
```

### **Find Issues by Component**
```powershell
# Audio-related issues
gh issue list --label "audio" --state open

# SIP protocol issues  
gh issue list --label "sip" --state open

# UI/UX improvements
gh issue list --label "ui" --state open
```

---

## 📈 **Project Health Monitoring**

### **Check Overall Status**
```powershell
# Quick health check
gh issue list --label "priority:critical" --state open | wc -l  # Should be 0
gh issue list --label "priority:high" --state open | wc -l     # Should be minimal

# View project progress
gh issue list --state open | head -20
gh issue list --state closed --limit 10
```

### **Monthly Review**
```powershell
# Issues created this month
gh issue list --state all --json createdAt,title,number | jq '.[] | select(.createdAt | startswith("2024-12"))'

# Issues closed this month  
gh issue list --state closed --json closedAt,title,number | jq '.[] | select(.closedAt | startswith("2024-12"))'
```

---

## 🆘 **Troubleshooting**

### **Common Issues**
- **Issue not auto-closing**: Ensure PR description contains `Fixes #X`
- **Can't find issue**: Use `gh issue list --state all` to search all issues
- **Branch not linked**: Use `gh issue develop X --checkout` instead of manual branch creation
- **Labels missing**: Add labels with `gh issue edit X --add-label "priority:high,sip"`

### **Getting Help**
```powershell
# View help for issue commands
gh issue --help
gh pr --help

# View specific command help
gh issue create --help
gh issue develop --help
```

---

## 📚 **Related Documentation**
- [GitHub Issues Migration Summary](./GITHUB_ISSUES_MIGRATION.md)
- [SIP Phone Instructions](./.github/instructions/SipPhoneInstructions.instructions.md)
- [Current Status Summary](./CURRENT_STATUS_SUMMARY.md)
- [Project Roadmap](./PROJECT_ROADMAP.md)

---

**✨ Remember: GitHub Issues is now the single source of truth for all bug tracking and feature management. No local files are used for tracking.**
