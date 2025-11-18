# Development Roadmap - Foundry Demo Agent Framework

> **Status**: Planning Phase  
> **Last Updated**: 2025-11-18  
> **Branch**: copilot/plan-next-steps

## Executive Summary

This document outlines the strategic development plan for the Foundry Demo Agent Framework. The project demonstrates persisted AI agents hosted in Azure AI Foundry with comprehensive Azure infrastructure for production deployment.

## Current State Assessment

### ✅ Strengths
- **Solid Foundation**: Working multi-agent orchestration system using Microsoft Agent Framework
- **Production Ready Infrastructure**: Complete Bicep templates for Azure deployment
- **MCP Integration**: Advanced Model Context Protocol integration for sentiment analysis
- **Build Status**: All projects build successfully
- **Test Status**: Existing tests (2 tests) pass successfully
- **Documentation**: Comprehensive README and architecture diagrams

### ⚠️ Areas for Improvement
- **Test Coverage**: Limited test coverage (only 2 test classes)
- **Security**: Known vulnerability in Moq 4.20.0 (GHSA-6r78-m64m-qwcf)
- **CI/CD**: No automated build/test/deploy pipeline
- **Monitoring**: Limited observability and metrics
- **Code Quality**: No automated code analysis tools

---

## Phase 1: Security & Dependencies ⚡ IMMEDIATE

**Priority**: CRITICAL  
**Estimated Effort**: 1-2 days  
**Dependencies**: None

### Tasks
- [ ] **Update Moq Package** (Critical)
  - Current: 4.20.0 (vulnerable)
  - Target: 4.20.72+ or consider alternatives (NSubstitute, FakeItEasy)
  - Impact: Fixes GHSA-6r78-m64m-qwcf vulnerability
  
- [ ] **Dependency Audit**
  - Run `dotnet list package --vulnerable`
  - Review all package versions for security patches
  - Document dependency update policy
  
- [ ] **Security Scanning Setup**
  - Add GitHub Dependabot configuration
  - Configure CodeQL security scanning
  - Set up vulnerability alerts

### Acceptance Criteria
- [ ] No known vulnerabilities in dependencies
- [ ] All tests pass after updates
- [ ] Automated security scanning enabled

---

## Phase 2: Test Coverage Expansion 🧪

**Priority**: HIGH  
**Estimated Effort**: 2-3 weeks  
**Dependencies**: Phase 1

### Current Coverage
- **RemoteDataAgent**: ✅ Basic tests exist
- **OrchestratorAgent**: ❌ No tests
- **EnergyAgent**: ❌ No tests
- **SentimentAgent**: ❌ No tests
- **EmailGenerator**: ❌ No tests
- **EmailAssistant**: ❌ No tests
- **CopilotStudio**: ❌ No tests
- **ReportAgent**: ❌ No tests

### Tasks

#### 2.1 Unit Tests for Core Agents
- [ ] **OrchestratorAgent Tests** (Priority 1)
  - [ ] Test agent pipeline execution order
  - [ ] Test conditional agent execution (SentimentAgent vs Energy pipeline)
  - [ ] Test error handling and recovery
  - [ ] Test envelope transformation logic
  - [ ] Test agent skipping logic
  
- [ ] **EnergyAgent Tests** (Priority 1)
  - [ ] Test baseline calculation logic
  - [ ] Test energy measure generation
  - [ ] Test JSON envelope parsing
  - [ ] Test Python plotting integration
  - [ ] Test error scenarios (invalid data, missing fields)
  
- [ ] **SentimentAgent Tests** (Priority 2)
  - [ ] Test MCP server integration
  - [ ] Test sentiment analysis responses
  - [ ] Test auto-approval system
  - [ ] Test error handling for MCP failures
  
- [ ] **EmailGenerator Tests** (Priority 2)
  - [ ] Test email template generation
  - [ ] Test attachment handling
  - [ ] Test base64 encoding
  - [ ] Test HTML email generation
  
- [ ] **EmailAssistant Tests** (Priority 2)
  - [ ] Test Logic App connector integration
  - [ ] Test email envelope transformation
  - [ ] Test error handling for send failures

#### 2.2 Integration Tests
- [ ] **End-to-End Pipeline Tests**
  - [ ] Full energy analysis workflow
  - [ ] Sentiment analysis standalone workflow
  - [ ] CopilotStudio integration workflow
  - [ ] Email sending workflow
  
- [ ] **MCP Integration Tests**
  - [ ] MCP server connectivity
  - [ ] Tool execution flow
  - [ ] Auto-approval mechanism

#### 2.3 Test Infrastructure
- [ ] Add test data fixtures
- [ ] Create test helper utilities
- [ ] Set up test coverage reporting (Coverlet)
- [ ] Add test result publishing
- [ ] Target: 70% code coverage minimum

### Acceptance Criteria
- [ ] All agents have unit tests
- [ ] Critical paths have integration tests
- [ ] Code coverage >= 70%
- [ ] Coverage reports generated in CI/CD
- [ ] All tests documented

---

## Phase 3: Code Quality Improvements 📊

**Priority**: MEDIUM  
**Estimated Effort**: 1-2 weeks  
**Dependencies**: Phase 2

### Tasks

#### 3.1 Code Standards & Analysis
- [ ] **Add EditorConfig**
  - Define C# coding standards
  - Configure indentation, line endings
  - Set naming conventions
  
- [ ] **Static Code Analysis**
  - Add StyleCop.Analyzers NuGet package
  - Configure Roslynator
  - Enable code analysis on build
  - Fix existing violations
  
- [ ] **Documentation Standards**
  - Add XML documentation to all public APIs
  - Configure documentation warnings as errors
  - Generate API documentation

#### 3.2 Code Refactoring
- [ ] **OrchestratorAgent Refactoring**
  - Break down large RunAsync method (1043 lines)
  - Extract agent execution logic into separate methods
  - Improve error handling with custom exceptions
  - Add cancellation token support
  
- [ ] **Logging Improvements**
  - Standardize logging across all agents
  - Add structured logging with Serilog
  - Define log levels consistently
  - Add correlation IDs for tracing
  
- [ ] **Error Handling**
  - Create custom exception types
  - Implement proper exception handling strategy
  - Add retry logic with Polly
  - Document error scenarios

#### 3.3 Code Organization
- [ ] Review and consolidate shared utilities
- [ ] Separate concerns (business logic vs infrastructure)
- [ ] Apply SOLID principles
- [ ] Add design pattern documentation

### Acceptance Criteria
- [ ] All code passes static analysis
- [ ] EditorConfig enforced in CI/CD
- [ ] Public APIs fully documented
- [ ] Logging standards applied consistently
- [ ] Code maintainability score > 80

---

## Phase 4: Documentation Enhancement 📚

**Priority**: MEDIUM  
**Estimated Effort**: 1-2 weeks  
**Dependencies**: Phase 3

### Tasks

#### 4.1 API Documentation
- [ ] Generate Swagger/OpenAPI docs for WebDemo
- [ ] Document REST API endpoints
- [ ] Add request/response examples
- [ ] Document authentication flows

#### 4.2 Architecture Documentation
- [ ] Create Architecture Decision Records (ADR)
  - [ ] ADR-001: Choice of Microsoft Agent Framework
  - [ ] ADR-002: MCP Integration Architecture
  - [ ] ADR-003: Azure Infrastructure Design
  - [ ] ADR-004: Agent Pipeline Design
  
- [ ] Enhance architecture diagrams
  - [ ] Add sequence diagrams for each workflow
  - [ ] Document data flow between agents
  - [ ] Visualize error handling paths

#### 4.3 Developer Guides
- [ ] **Onboarding Guide**
  - Prerequisites and setup
  - First-time build instructions
  - Development environment setup
  - Common pitfalls and solutions
  
- [ ] **Agent Development Guide**
  - How to create a new agent
  - Agent instruction file format
  - Testing strategies
  - Best practices
  
- [ ] **Troubleshooting Guide**
  - Common errors and solutions
  - Debugging techniques
  - Log analysis guide
  - Performance profiling

#### 4.4 User Documentation
- [ ] Web Demo user guide
- [ ] Console application usage
- [ ] Configuration reference
- [ ] Deployment guide enhancements

### Acceptance Criteria
- [ ] All APIs documented
- [ ] ADRs created for major decisions
- [ ] Developer onboarding guide complete
- [ ] Troubleshooting guide covers common issues
- [ ] Documentation reviewed and approved

---

## Phase 5: Infrastructure & DevOps 🚀

**Priority**: HIGH  
**Estimated Effort**: 2-3 weeks  
**Dependencies**: Phase 1, Phase 2

### Tasks

#### 5.1 Continuous Integration
- [ ] **GitHub Actions CI Pipeline**
  - [ ] Build validation
  - [ ] Test execution
  - [ ] Code coverage reporting
  - [ ] Static code analysis
  - [ ] Security scanning
  
- [ ] **PR Validation**
  - [ ] Automated build checks
  - [ ] Test requirements
  - [ ] Coverage thresholds
  - [ ] Code review automation

#### 5.2 Continuous Deployment
- [ ] **Container Images**
  - [ ] Add Dockerfile for Foundry.Agents
  - [ ] Add Dockerfile for WebDemo
  - [ ] Set up container registry (Azure ACR)
  - [ ] Automate image building
  
- [ ] **Azure Deployment Automation**
  - [ ] Automate Bicep deployment
  - [ ] Environment-specific configurations
  - [ ] Secret management automation
  - [ ] Rollback procedures

#### 5.3 Release Management
- [ ] Implement semantic versioning (SemVer)
- [ ] Add changelog automation (conventional commits)
- [ ] Create release notes templates
- [ ] Tag releases automatically
- [ ] Publish releases to GitHub

#### 5.4 Monitoring & Observability
- [ ] Application Insights integration (already started)
- [ ] Custom metrics and dashboards
- [ ] Alert rules configuration
- [ ] Log aggregation setup
- [ ] Performance baselines

### Acceptance Criteria
- [ ] CI pipeline runs on every PR
- [ ] Automated deployment to dev environment
- [ ] Container images published automatically
- [ ] Monitoring dashboards operational
- [ ] Release process documented and tested

---

## Phase 6: Feature Enhancements ✨

**Priority**: MEDIUM  
**Estimated Effort**: 3-4 weeks  
**Dependencies**: Phase 2, Phase 5

### Tasks

#### 6.1 Agent System Improvements
- [ ] **Health Monitoring**
  - [ ] Add health check endpoints
  - [ ] Agent status monitoring
  - [ ] Dependency health checks
  - [ ] Metrics collection
  
- [ ] **Reliability Improvements**
  - [ ] Implement retry logic with Polly
  - [ ] Add circuit breaker pattern
  - [ ] Implement timeout handling
  - [ ] Add fallback mechanisms
  
- [ ] **Performance Optimizations**
  - [ ] Add response caching (RemoteDataAgent)
  - [ ] Optimize JSON serialization
  - [ ] Connection pooling for external services
  - [ ] Batch processing support

#### 6.2 Agent Versioning
- [ ] Design agent versioning strategy
- [ ] Implement version tracking
- [ ] Support multiple agent versions
- [ ] Version migration tools

#### 6.3 Parallel Execution
- [ ] Identify agents that can run in parallel
- [ ] Implement parallel execution in Orchestrator
- [ ] Add dependency graph support
- [ ] Performance benchmarking

#### 6.4 Agent Templating
- [ ] Create agent template system
- [ ] Add scaffolding CLI tool
- [ ] Generate agent boilerplate
- [ ] Template best practices guide

### Acceptance Criteria
- [ ] Health checks return accurate status
- [ ] Retry logic handles transient failures
- [ ] Caching improves performance by 30%+
- [ ] Agent versioning works end-to-end
- [ ] Agent creation time reduced by 50%+

---

## Phase 7: Performance Optimization ⚡

**Priority**: LOW  
**Estimated Effort**: 2-3 weeks  
**Dependencies**: Phase 6

### Tasks

#### 7.1 Performance Profiling
- [ ] Profile agent execution times
- [ ] Identify bottlenecks
- [ ] Memory usage analysis
- [ ] Network call optimization

#### 7.2 Optimization Implementation
- [ ] Optimize hot paths
- [ ] Reduce memory allocations
- [ ] Minimize API calls
- [ ] Implement lazy loading

#### 7.3 Load Testing
- [ ] Create load test scenarios
- [ ] Establish performance baselines
- [ ] Run stress tests
- [ ] Document performance characteristics

### Acceptance Criteria
- [ ] Performance baselines established
- [ ] Critical paths optimized
- [ ] Load test results documented
- [ ] Performance targets met

---

## Phase 8: User Experience Enhancements 🎨

**Priority**: LOW  
**Estimated Effort**: 2-3 weeks  
**Dependencies**: Phase 6

### Tasks

#### 8.1 Web Demo Enhancements
- [ ] Add real-time progress indicators
- [ ] Implement execution history view
- [ ] Add result filtering and search
- [ ] Improve mobile responsiveness
- [ ] Add dark mode support

#### 8.2 Multi-tenancy Support
- [ ] Add user authentication
- [ ] Implement tenant isolation
- [ ] Add role-based access control
- [ ] Secure agent configurations

#### 8.3 Configuration UI
- [ ] Build agent configuration interface
- [ ] Add validation
- [ ] Support configuration export/import
- [ ] Version configuration changes

#### 8.4 CLI Tool
- [ ] Create management CLI
- [ ] Add agent management commands
- [ ] Support scripting scenarios
- [ ] Add shell completion

### Acceptance Criteria
- [ ] Web Demo has improved UX
- [ ] Multi-tenancy fully functional
- [ ] Configuration UI intuitive
- [ ] CLI tool feature-complete

---

## Success Metrics

### Quality Metrics
- **Code Coverage**: >= 70%
- **Build Success Rate**: >= 95%
- **Static Analysis Violations**: 0 critical, < 10 warnings
- **Security Vulnerabilities**: 0 known vulnerabilities
- **Documentation Coverage**: 100% of public APIs

### Performance Metrics
- **Build Time**: < 5 minutes
- **Test Execution Time**: < 2 minutes
- **Agent Pipeline Execution**: < 30 seconds (typical scenario)
- **API Response Time**: p95 < 500ms

### DevOps Metrics
- **Deployment Frequency**: On-demand (target: daily)
- **Lead Time for Changes**: < 1 day
- **Mean Time to Recovery**: < 1 hour
- **Change Failure Rate**: < 5%

---

## Risk Assessment

### High Risk Items
- **Security Vulnerability in Moq**: Immediate fix required
- **Azure Cost Overruns**: Monitor and set budget alerts
- **Breaking API Changes**: Implement versioning strategy

### Medium Risk Items
- **Test Infrastructure Gaps**: Address in Phase 2
- **Documentation Debt**: Address in Phase 4
- **Performance Bottlenecks**: Profile in Phase 7

### Mitigation Strategies
- Prioritize security fixes
- Implement monitoring and alerting early
- Regular code reviews
- Incremental improvements with validation

---

## Timeline Estimate

| Phase | Duration | Dependencies | Start | End |
|-------|----------|--------------|-------|-----|
| Phase 1: Security | 1-2 days | None | Week 1 | Week 1 |
| Phase 2: Testing | 2-3 weeks | Phase 1 | Week 1 | Week 4 |
| Phase 3: Code Quality | 1-2 weeks | Phase 2 | Week 4 | Week 6 |
| Phase 4: Documentation | 1-2 weeks | Phase 3 | Week 6 | Week 8 |
| Phase 5: DevOps | 2-3 weeks | Phase 1, 2 | Week 2 | Week 5 |
| Phase 6: Features | 3-4 weeks | Phase 2, 5 | Week 6 | Week 10 |
| Phase 7: Performance | 2-3 weeks | Phase 6 | Week 10 | Week 13 |
| Phase 8: UX | 2-3 weeks | Phase 6 | Week 11 | Week 14 |

**Total Timeline**: ~14 weeks (3.5 months) for full completion

---

## Next Actions (Immediate)

1. **Update Moq Package** - Fix security vulnerability
2. **Set up GitHub Actions** - Automate build and test
3. **Add Orchestrator Tests** - Start expanding test coverage
4. **Create ADR-001** - Document framework choice
5. **Enable Dependabot** - Automate dependency updates

---

## Review Schedule

- **Weekly**: Review progress against plan
- **Bi-weekly**: Adjust priorities based on feedback
- **Monthly**: Update metrics and timeline
- **Quarterly**: Major plan review and adjustments

---

## Approval & Sign-off

| Role | Name | Date | Status |
|------|------|------|--------|
| Product Owner | TBD | TBD | Pending |
| Tech Lead | TBD | TBD | Pending |
| DevOps Lead | TBD | TBD | Pending |

---

## Change Log

| Date | Version | Changes | Author |
|------|---------|---------|--------|
| 2025-11-18 | 1.0 | Initial plan created | Copilot Agent |

---

## References

- [Microsoft Agent Framework Documentation](https://learn.microsoft.com/en-us/azure/ai-studio/)
- [Azure AI Foundry](https://learn.microsoft.com/en-us/azure/ai-foundry/)
- [Model Context Protocol (MCP)](https://github.com/modelcontextprotocol)
- [Azure Bicep Documentation](https://learn.microsoft.com/en-us/azure/azure-resource-manager/bicep/)

---

> 💡 **Note**: This plan is a living document and should be updated regularly as the project evolves and requirements change.
