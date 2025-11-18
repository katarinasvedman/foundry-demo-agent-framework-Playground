# Scripts Directory

This directory contains utility scripts for the Foundry Demo Agent Framework project.

## Available Scripts

### create-issues.sh

**Purpose**: Automates creation of GitHub Issues for the development roadmap.

**What it does**:
- Creates all necessary GitHub labels (priorities, types, phases)
- Creates Epic issues for each development phase
- Creates sub-issues for key tasks
- Links sub-issues to their parent epics

**Prerequisites**:
- GitHub CLI (`gh`) installed and authenticated
- Write access to the repository

**Usage**:
```bash
# Navigate to repository root
cd /home/runner/work/foundry-demo-agent-framework-Playground/foundry-demo-agent-framework-Playground

# Run the script
./scripts/create-issues.sh
```

**What gets created**:
The script creates a **subset** of the full issue structure as a starting point:
- 4 Epic issues (Security, Testing, DevOps, Quick Wins)
- ~12 sub-issues covering critical tasks

**Note**: This script creates example issues only. See `ISSUES.md` for the complete list of ~85-90 issues to create.

**Manual alternative**:
If you prefer to create issues manually or need to create the remaining issues, use `ISSUES.md` as your reference. It contains complete descriptions, task lists, and acceptance criteria for all issues.

## Installation of GitHub CLI

If you don't have GitHub CLI installed:

### On macOS:
```bash
brew install gh
```

### On Windows:
```powershell
winget install --id GitHub.cli
```

### On Linux:
```bash
# Debian/Ubuntu
sudo apt install gh

# Fedora/RHEL
sudo dnf install gh

# Arch Linux
sudo pacman -S github-cli
```

### Authentication:
```bash
gh auth login
```

Follow the prompts to authenticate with your GitHub account.

## Future Scripts

Additional scripts that could be added:
- `setup-dev-environment.sh` - Set up local development environment
- `run-all-tests.sh` - Run all test suites with coverage
- `deploy-dev.sh` - Deploy to development environment
- `update-dependencies.sh` - Update all dependencies safely

## Contributing

When adding new scripts:
1. Make scripts executable: `chmod +x scripts/your-script.sh`
2. Add error handling and validation
3. Include usage instructions in this README
4. Test scripts before committing
5. Add appropriate comments in the script

## Support

For issues with scripts:
1. Check script prerequisites are met
2. Review error messages carefully
3. See TROUBLESHOOTING.md (when available)
4. Open an issue in GitHub

---

> Last Updated: 2025-11-18
