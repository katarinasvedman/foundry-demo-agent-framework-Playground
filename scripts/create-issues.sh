#!/bin/bash

# GitHub Issues Creation Script
# This script creates all epics and sub-issues defined in ISSUES.md
# 
# Prerequisites:
# - GitHub CLI (gh) installed and authenticated
# - Appropriate repository permissions
#
# Usage:
#   ./scripts/create-issues.sh

set -e

# Configuration
REPO="katarinasvedman/foundry-demo-agent-framework-Playground"
LABEL_PREFIX=""

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

# Function to print colored output
print_info() {
    echo -e "${BLUE}[INFO]${NC} $1"
}

print_success() {
    echo -e "${GREEN}[SUCCESS]${NC} $1"
}

print_warning() {
    echo -e "${YELLOW}[WARNING]${NC} $1"
}

print_error() {
    echo -e "${RED}[ERROR]${NC} $1"
}

# Function to create labels if they don't exist
create_labels() {
    print_info "Creating labels..."
    
    # Priority labels
    gh label create "P0-critical" --description "Critical priority" --color "d73a4a" --repo $REPO 2>/dev/null || true
    gh label create "P0-high" --description "High priority" --color "d93f0b" --repo $REPO 2>/dev/null || true
    gh label create "P1-high" --description "High priority" --color "fb8500" --repo $REPO 2>/dev/null || true
    gh label create "P1-medium" --description "Medium priority" --color "fbca04" --repo $REPO 2>/dev/null || true
    gh label create "P2-medium" --description "Medium priority" --color "ffd60a" --repo $REPO 2>/dev/null || true
    gh label create "P2-low" --description "Low priority" --color "0e8a16" --repo $REPO 2>/dev/null || true
    
    # Type labels
    gh label create "epic" --description "Epic issue" --color "5319e7" --repo $REPO 2>/dev/null || true
    gh label create "security" --description "Security related" --color "d73a4a" --repo $REPO 2>/dev/null || true
    gh label create "testing" --description "Testing related" --color "1d76db" --repo $REPO 2>/dev/null || true
    gh label create "documentation" --description "Documentation" --color "0075ca" --repo $REPO 2>/dev/null || true
    gh label create "devops" --description "DevOps/Infrastructure" --color "0052cc" --repo $REPO 2>/dev/null || true
    gh label create "code-quality" --description "Code quality" --color "c5def5" --repo $REPO 2>/dev/null || true
    gh label create "feature" --description "New feature" --color "a2eeef" --repo $REPO 2>/dev/null || true
    gh label create "performance" --description "Performance" --color "d4c5f9" --repo $REPO 2>/dev/null || true
    gh label create "ux" --description "User Experience" --color "e99695" --repo $REPO 2>/dev/null || true
    gh label create "quick-win" --description "Quick win" --color "bfd4f2" --repo $REPO 2>/dev/null || true
    
    # Phase labels
    gh label create "phase-1" --description "Phase 1: Security" --color "f9d0c4" --repo $REPO 2>/dev/null || true
    gh label create "phase-2" --description "Phase 2: Testing" --color "f9d0c4" --repo $REPO 2>/dev/null || true
    gh label create "phase-3" --description "Phase 3: Code Quality" --color "f9d0c4" --repo $REPO 2>/dev/null || true
    gh label create "phase-4" --description "Phase 4: Documentation" --color "f9d0c4" --repo $REPO 2>/dev/null || true
    gh label create "phase-5" --description "Phase 5: DevOps" --color "f9d0c4" --repo $REPO 2>/dev/null || true
    gh label create "phase-6" --description "Phase 6: Features" --color "f9d0c4" --repo $REPO 2>/dev/null || true
    gh label create "phase-7" --description "Phase 7: Performance" --color "f9d0c4" --repo $REPO 2>/dev/null || true
    gh label create "phase-8" --description "Phase 8: UX" --color "f9d0c4" --repo $REPO 2>/dev/null || true
    
    # Component labels
    gh label create "dependencies" --description "Dependencies" --color "0366d6" --repo $REPO 2>/dev/null || true
    gh label create "automation" --description "Automation" --color "0366d6" --repo $REPO 2>/dev/null || true
    gh label create "infrastructure" --description "Infrastructure" --color "0366d6" --repo $REPO 2>/dev/null || true
    gh label create "ci" --description "Continuous Integration" --color "0366d6" --repo $REPO 2>/dev/null || true
    gh label create "cd" --description "Continuous Deployment" --color "0366d6" --repo $REPO 2>/dev/null || true
    
    print_success "Labels created"
}

# Array to store epic issue numbers
declare -A EPIC_NUMBERS

# Function to create an epic
create_epic() {
    local epic_id=$1
    local title=$2
    local body=$3
    local labels=$4
    
    print_info "Creating Epic $epic_id: $title"
    
    issue_number=$(gh issue create \
        --repo $REPO \
        --title "$title" \
        --body "$body" \
        --label "$labels" | grep -oP '\d+$')
    
    EPIC_NUMBERS[$epic_id]=$issue_number
    print_success "Created Epic $epic_id as issue #$issue_number"
    echo $issue_number
}

# Function to create a sub-issue
create_sub_issue() {
    local title=$1
    local body=$2
    local labels=$3
    local epic_ref=$4
    
    print_info "Creating issue: $title"
    
    # Add epic reference to body
    if [ -n "$epic_ref" ] && [ -n "${EPIC_NUMBERS[$epic_ref]}" ]; then
        body="${body}\n\n---\n**Epic**: #${EPIC_NUMBERS[$epic_ref]}"
    fi
    
    gh issue create \
        --repo $REPO \
        --title "$title" \
        --body "$body" \
        --label "$labels" > /dev/null
    
    print_success "Created: $title"
}

# Main execution
main() {
    print_info "Starting GitHub Issues creation for $REPO"
    echo ""
    
    # Check if gh is installed
    if ! command -v gh &> /dev/null; then
        print_error "GitHub CLI (gh) is not installed. Please install it first."
        exit 1
    fi
    
    # Check if authenticated
    if ! gh auth status &> /dev/null; then
        print_error "Not authenticated with GitHub CLI. Please run 'gh auth login' first."
        exit 1
    fi
    
    # Create labels
    create_labels
    echo ""
    
    # Epic 1: Security & Dependencies
    print_info "=== Creating Epic 1: Security & Dependencies ==="
    epic1_body="Address security vulnerabilities and establish dependency management practices.

## Acceptance Criteria
- [ ] No known security vulnerabilities in dependencies
- [ ] All tests pass after updates
- [ ] Automated security scanning enabled
- [ ] Dependency update policy documented

## Sub-Issues
This epic contains 5 sub-issues covering security fixes, dependency audits, and automation setup.

See ISSUES.md for complete details."

    create_epic "epic1" "Epic 1: Security & Dependencies" "$epic1_body" "epic,security,P0-critical,phase-1"
    
    # Epic 1 Sub-issues
    create_sub_issue \
        "Update Moq Package to Fix GHSA-6r78-m64m-qwcf" \
        "Update Moq package from 4.20.0 to 4.20.72+ to fix security vulnerability.

## Tasks
- [ ] Update Moq package in tests/Foundry.Agents.Tests/Foundry.Agents.Tests.csproj
- [ ] Run all tests to verify compatibility
- [ ] Verify no security warnings
- [ ] Document the change

## Acceptance Criteria
- [ ] Moq version >= 4.20.72
- [ ] All tests pass
- [ ] No security vulnerabilities reported

**Estimated Time**: 30 minutes" \
        "security,dependencies,P0-critical" \
        "epic1"
    
    create_sub_issue \
        "Run Comprehensive Dependency Audit" \
        "Audit all NuGet packages for security vulnerabilities and outdated versions.

## Tasks
- [ ] Run dotnet list package --vulnerable --include-transitive
- [ ] Document all vulnerabilities found
- [ ] Create issues for each vulnerability
- [ ] Run dotnet list package --outdated
- [ ] Document update recommendations

**Estimated Time**: 1 hour" \
        "security,dependencies,P0-critical" \
        "epic1"
    
    create_sub_issue \
        "Configure GitHub Dependabot" \
        "Enable automated dependency updates using GitHub Dependabot.

## Tasks
- [ ] Create .github/dependabot.yml configuration
- [ ] Configure NuGet package ecosystem
- [ ] Set weekly update schedule
- [ ] Configure PR limits and labels
- [ ] Verify Dependabot PRs start appearing

**Estimated Time**: 15 minutes" \
        "automation,dependencies,P0-critical" \
        "epic1"
    
    create_sub_issue \
        "Set Up CodeQL Security Scanning" \
        "Configure automated security scanning using GitHub CodeQL.

## Tasks
- [ ] Create .github/workflows/codeql.yml
- [ ] Configure for C# language
- [ ] Set up scanning triggers
- [ ] Configure alert notifications
- [ ] Review and address initial findings

**Estimated Time**: 1 hour" \
        "security,automation,P0-critical" \
        "epic1"
    
    create_sub_issue \
        "Document Dependency Update Policy" \
        "Create documentation for dependency update and review process.

## Tasks
- [ ] Document update frequency policy
- [ ] Define testing requirements
- [ ] Document security response process
- [ ] Define approval process
- [ ] Add policy to documentation

**Estimated Time**: 30 minutes" \
        "documentation,process,P1-high" \
        "epic1"
    
    echo ""
    
    # Epic 2: Test Coverage Expansion
    print_info "=== Creating Epic 2: Test Coverage Expansion ==="
    epic2_body="Expand test coverage from current <20% to minimum 70%.

## Acceptance Criteria
- [ ] All agents have unit test coverage
- [ ] Critical workflows have integration tests
- [ ] Code coverage >= 70%
- [ ] Coverage reporting enabled in CI/CD
- [ ] Test documentation complete

## Sub-Issues
This epic contains 11 sub-issues covering unit tests for all agents and integration tests.

See ISSUES.md for complete details."

    create_epic "epic2" "Epic 2: Test Coverage Expansion" "$epic2_body" "epic,testing,P0-high,phase-2"
    
    # Create first few test issues as examples
    create_sub_issue \
        "Set Up Test Coverage Reporting" \
        "Configure Coverlet and code coverage reporting in CI/CD.

## Tasks
- [ ] Add Coverlet.Collector package
- [ ] Update CI workflow to collect coverage
- [ ] Configure coverage report format
- [ ] Set up Codecov or similar service
- [ ] Add coverage badge to README

**Estimated Time**: 1 hour" \
        "testing,infrastructure,P0-high" \
        "epic2"
    
    create_sub_issue \
        "Add OrchestratorAgent Unit Tests" \
        "Create comprehensive unit tests for OrchestratorAgent.

## Tasks
- [ ] Create OrchestratorAgentTests.cs
- [ ] Test agent pipeline construction
- [ ] Test conditional execution logic
- [ ] Test envelope transformation
- [ ] Test error handling scenarios
- [ ] Target 80%+ coverage

**Estimated Time**: 8 hours" \
        "testing,orchestrator,P0-high" \
        "epic2"
    
    create_sub_issue \
        "Add EnergyAgent Unit Tests" \
        "Create unit tests for EnergyAgent calculation logic.

## Tasks
- [ ] Create EnergyAgentTests.cs
- [ ] Test baseline calculation
- [ ] Test energy measure generation
- [ ] Test JSON envelope parsing
- [ ] Test error scenarios
- [ ] Target 70%+ coverage

**Estimated Time**: 6 hours" \
        "testing,energy-agent,P0-high" \
        "epic2"
    
    print_info "Note: Only creating first 3 test issues. See ISSUES.md for remaining 8 test issues."
    echo ""
    
    # Epic 5: Infrastructure & DevOps
    print_info "=== Creating Epic 5: Infrastructure & DevOps ==="
    epic5_body="Establish CI/CD automation, containerization, and monitoring.

## Acceptance Criteria
- [ ] CI pipeline runs on every PR
- [ ] Automated deployment to dev environment
- [ ] Container images published
- [ ] Monitoring dashboards operational
- [ ] Release process documented

## Sub-Issues
This epic contains 10 sub-issues covering CI/CD, containers, and monitoring.

See ISSUES.md for complete details."

    create_epic "epic5" "Epic 5: Infrastructure & DevOps" "$epic5_body" "epic,devops,infrastructure,P0-high,phase-5"
    
    create_sub_issue \
        "Create GitHub Actions CI Workflow" \
        "Create comprehensive CI workflow for build, test, and validation.

## Tasks
- [ ] Create .github/workflows/ci.yml
- [ ] Configure triggers (push, PR)
- [ ] Add .NET setup, restore, build steps
- [ ] Add test step with coverage
- [ ] Add vulnerability check
- [ ] Test workflow

**Estimated Time**: 2 hours" \
        "devops,ci,P0-high" \
        "epic5"
    
    create_sub_issue \
        "Create Dockerfile for Foundry.Agents" \
        "Create production-ready Dockerfile with multi-stage build.

## Tasks
- [ ] Create src/Foundry.Agents/Dockerfile
- [ ] Implement multi-stage build
- [ ] Optimize image size
- [ ] Add health check
- [ ] Test container locally
- [ ] Document usage

**Estimated Time**: 2 hours" \
        "devops,docker,P0-high" \
        "epic5"
    
    print_info "Note: Only creating first 2 DevOps issues. See ISSUES.md for remaining 8 DevOps issues."
    echo ""
    
    # Epic 9: Quick Wins
    print_info "=== Creating Epic 9: Quick Wins ==="
    epic9_body="High-impact tasks that can be completed quickly to deliver immediate value.

## Acceptance Criteria
- [ ] All 10 quick wins completed
- [ ] Immediate value delivered
- [ ] Foundation set for future work

## Sub-Issues
This epic contains 10 high-impact tasks from QUICK_WINS.md.

See ISSUES.md and QUICK_WINS.md for complete details."

    create_epic "epic9" "Epic 9: Quick Wins" "$epic9_body" "epic,quick-wins,P0-critical"
    
    print_info "Note: Quick win sub-issues reference issues from other epics."
    echo ""
    
    # Summary
    print_info "=== Issue Creation Summary ==="
    echo ""
    print_success "Created Epics:"
    for epic_id in "${!EPIC_NUMBERS[@]}"; do
        echo "  - $epic_id: Issue #${EPIC_NUMBERS[$epic_id]}"
    done
    echo ""
    
    print_warning "Note: This script created a subset of issues for demonstration."
    print_warning "See ISSUES.md for the complete list of ~85-90 issues to create."
    echo ""
    
    print_info "Next Steps:"
    echo "  1. Review created issues in GitHub"
    echo "  2. Create remaining sub-issues from ISSUES.md"
    echo "  3. Set up GitHub Project board"
    echo "  4. Link dependencies between issues"
    echo "  5. Begin with Quick Wins (Epic 9)"
    echo ""
    
    print_success "Issue creation completed!"
}

# Run main function
main
