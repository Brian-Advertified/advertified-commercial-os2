# POPIA Compliance for Advertified Unified

## Legal Context

**Deployment Region**: AWS af-south-1 (Cape Town, South Africa)
**Governing Law**: Protection of Personal Information Act 4 of 2013 (POPIA)
**Currency**: ZAR (South African Rand)
**Commercial Context**: South African advertising and marketing operations

## POPIA Principles Implementation

### 1. Lawfulness, Fairness, and Transparency (Section 11)

**Implementation**:
- Clear privacy policy accessible from all authenticated screens
- Explicit consent basis for each data category collected
- Purpose limitation: data collected only for specified commercial purposes
- Privacy notice before data collection for contacts, profiles, audience data

**Evidence requirements**:
- Privacy policy versioned and approved by legal
- Consent capture date, purpose, and basis recorded for each data subject
- Privacy impact assessment completed before production data collection

### 2. Purpose Limitation (Section 13)

**Data categories and purposes**:

| Data Category | Primary Purpose | Secondary Purpose | Retention Period |
|---------------|-----------------|-------------------|------------------|
| Contact information (email, phone) | Campaign communication, RFQ responses | Account management | 24 months post-relationship |
| Audience demographic data | Campaign planning, targeting | Performance analysis | 5 years post-campaign |
| Supplier commercial data | Inventory management, RFQs | Market analysis | 5 years post-relationship |
| Campaign performance data | Client reporting, optimisation | Benchmarking | 5 years post-campaign |
| User authentication data | Access control | Security audit | 12 months (security logs) |

**Implementation**:
- Each data field tagged with purpose in schema
- Cross-purpose use requires explicit consent or legal basis
- Automated retention jobs enforce deletion timelines

### 3. Data Minimisation (Section 10)

**Collection limits**:
- Collect only data necessary for stated purpose
- No consumer identity graph in initial release (explicit non-goal)
- Audience data as aggregate planning evidence only, not individual profiles
- Contact data limited to business context (no personal social media handles)

**Implementation**:
- Schema validation enforces required vs optional fields
- UI prevents collection of non-essential data
- API rejects requests with unnecessary data fields

### 4. Information Quality (Section 11)

**Data accuracy controls**:
- Supplier data requires evidence and verification levels
- Audience hypotheses labelled as such, not presented as facts
- Inventory rates require source evidence and freshness tracking
- Performance data includes methodology and limitations

**Implementation**:
- Data quality scores recorded with each record
- Automated validation rules for commercial data
- Human review required for material claims

### 5. Openness (Section 18)

**Transparency requirements**:
- Public privacy policy with specific data categories
- Data subject access request process
- Breach notification procedure (72-hour requirement)
- Processing register maintained

**Implementation**:
- Privacy policy accessible from footer of all authenticated screens
- Data subject request workflow in admin panel
- Automated breach detection and notification system
- Processing register in compliance documentation

### 6. Security Safeguards (Section 19)

**Technical measures**:
- Tenant isolation enforced at database and API level
- Encryption at rest (RDS encryption, S3 server-side encryption)
- Encryption in transit (TLS 1.2+)
- Access logging for all personal data access
- Regular security assessments

**Organisational measures**:
- Staff training on POPIA requirements
- Data protection officer designation
- Incident response procedure
- Third-party processor agreements (Data Processing Agreements)

### 7. Data Subject Participation (Sections 23-25)

**Rights implementation**:

| Right | Implementation | Owner |
|-------|----------------|--------|
| Access (Section 23) | Self-service data export + admin-assisted requests | Privacy Lead |
| Correction (Section 24) | Self-service for profile data, admin-assisted for audit records | Privacy Lead |
| Destruction (Section 25) | Automated retention jobs + admin-assisted requests | Privacy Lead |
| Objection to processing | Workflow for processing objections | Privacy Lead |

**Implementation**:
- Data subject request workflow in admin panel
- Automated export for own data (user-accessible)
- Admin workflow for sensitive data requests
- 30-day response time tracked in system

## Cross-Border Data Transfer

**Policy**: No cross-border data transfer without explicit legal basis

**Implementation**:
- AWS af-south-1 region ensures data stays in South Africa
- No international S3 buckets or databases
- Third-party providers must have South African presence or adequate adequacy finding
- Data Processing Agreements with all third-party processors

## Specific Data Categories

### Contact Data (Section 20)

**Data collected**: Name, role, email, phone, consent basis

**Purpose**: Campaign communication, RFQ responses, account management

**Legal basis**: Consent (business context), legitimate interest (existing relationship)

**Retention**: 24 months after relationship end

**Access**: Data subject can view and correct own contact data

### Audience/Segment Data

**Data collected**: Aggregate demographic segments, buying occasions, geographic patterns

**Purpose**: Campaign planning, targeting analysis

**Legal basis**: Legitimate interest (aggregate analysis), consent where applicable

**Retention**: 5 years post-campaign

**Special handling**: No individual consumer profiles, aggregate planning evidence only

### Supplier Commercial Data

**Data collected**: Company details, contacts, rates, inventory

**Purpose**: Inventory management, RFQs, marketplace operations

**Legal basis**: Contract, legitimate interest (business relationship)

**Retention**: 5 years post-relationship

**Special handling**: Supplier controls own data through tenant isolation

### Performance Data

**Data collected**: Campaign metrics, delivery data, outcomes

**Purpose**: Client reporting, optimisation, benchmarking

**Legal basis**: Contract, legitimate interest (performance analysis)

**Retention**: 5 years post-campaign

**Special handling**: Methodology and limitations disclosed

## Processing Register

**Required entries for each processing activity**:

1. **Processing activity name**
2. **Purpose** (specific business purpose)
3. **Legal basis** (consent, contract, legitimate interest, etc.)
4. **Data categories** (specific data fields)
5. **Data subjects** (contact types, roles, etc.)
6. **Recipients** (internal teams, third parties)
7. **Storage location** (specific AWS services, regions)
8. **Retention period** (specific timeline)
9. **Security measures** (technical and organisational)
10. **Owner** (responsible business owner)

**Implementation**:
- Processing register maintained in compliance documentation
- Version-controlled with approval dates
- Reviewed quarterly and updated for changes

## Incident Response

**Breach notification requirements**:
- 72-hour notification to Information Regulator
- Affected data subjects notified without undue delay
- Breach log maintained with details, response, and outcomes

**Implementation**:
- Automated breach detection system
- Incident response procedure documented
- Breach notification templates prepared
- Regular breach response drills

## Launch Requirements

**Before production data collection**:

1. ✅ Privacy policy approved by legal counsel
2. ✅ Privacy impact assessment completed
3. ✅ Processing register completed
4. ✅ Data subject request workflow implemented
5. ✅ Breach notification system operational
6. ✅ Staff training completed
7. ✅ Data Processing Agreements with third parties
8. ✅ Security controls verified (tenant isolation, encryption, access logging)
9. ✅ Retention jobs implemented and tested
10. ✅ Cross-border transfer policy confirmed

## Documentation Requirements

**POPIA compliance documentation**:
- Privacy policy (public-facing)
- Privacy impact assessment (internal)
- Processing register (internal)
- Data subject request procedure (internal)
- Breach notification procedure (internal)
- Security measures documentation (internal)
- Third-party processor agreements (contractual)
- Staff training records (internal)

## Ongoing Compliance

**Regular activities**:
- Quarterly review of processing register
- Annual privacy impact assessment update
- Regular security assessments
- Annual staff training refresh
- Bi-annual retention job verification
- Ongoing monitoring of regulatory changes

**Owner**: Privacy Lead (to be designated)
**Legal counsel**: External POPIA specialist (to be engaged)
**Timeline**: Compliance activities integrated into Gate 12 (Hardening)