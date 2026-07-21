# API Versioning Policy

This document defines the rules, classification, and procedures for API versioning and deprecation at TMS.

---

## 🚦 1. Classification of Changes

### 🔴 What Counts as a Breaking Change (Requires New Version)
Any change that breaks an existing client contract requires incrementing the API version (e.g. V1 to V2). This includes:
* **Payload Alterations**: Removing or renaming JSON fields in request or response bodies.
* **Strictness Increase**: Tightening validation rules (e.g., changing an optional field to required, changing format constraints).
* **Behavior/Status Changes**: Modifying HTTP response status codes returned for specific scenarios.
* **Ordering**: Changing default sorting order of paginated responses.

### 🟢 What Counts as Additive / Non-Breaking (Safe for Current Version)
Changes that can be rolled out seamlessly without breaking existing clients:
* **Additions**: Adding new optional query parameters, new optional request fields, or new response fields.
* **New Endpoints**: Exposing brand-new endpoints or HTTP verbs that did not exist before.

---

## 🌅 2. Deprecation and Sunset Strategy

We treat our API as a contract. When a version is deprecated, we follow a strict sunset lifecycle:

1. **Sunset Window**: A deprecated API version will remain active and fully supported for a minimum of **6 months** after its successor version is released.
2. **HTTP Header Signaling**:
   * `Deprecation: true`: Officially signals that the endpoint/version is deprecated.
   * `Sunset: <RFC 7231 Date>`: Specifies the exact date when the version will stop responding.
   * `Link: < successor version URI >; rel="successor-version"`: Provides the migration endpoint for clients.
3. **Communication**:
   * Day-one release notes and CHANGELOG entries detailing the migration steps.
   * Direct email notifications sent to all internal and external API key holders.
   * Calendar invite blocker sent for the final decommission date.

---

## ⏭️ 3. Version Skipping

* Clients are **not** forced to migrate through intermediate versions.
* For example, a client running on **V1** is permitted and encouraged to skip directly to **V3** once V3 is available, bypassing V2 entirely.
