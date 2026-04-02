# Microsoft Graph Permissions Guide - APITeamsV3

To enable automated provisioning of Microsoft Teams for Education (Class Teams) with custom naming and multi-owner support, the following **Application** permissions must be granted to the Azure AD App Registration.

## Required Permissions (Application Type)

| Permission | Description | Why we need it |
| :--- | :--- | :--- |
| **`EduRoster.ReadWrite.All`** | Read and write the organization's roster | Mandatory for creating and managing **Education Classes** (which are the foundation for Class Teams). |
| **`Group.ReadWrite.All`** | Read and write all groups | Necessary for managing the underlying M365 Group and assigning Owners/Members. |
| **`User.Read.All`** | Read all users' full profiles | Required to resolve owner/facilitator emails into their unique Graph Object IDs. |
| **`Directory.Read.All`** | Read directory data | Recommended for stable cross-resource resolution. |

## Important: Admin Consent
After adding these permissions in the **Azure Portal > App registrations > [Your App] > API permissions**, a global administrator **MUST** click the **"Grant admin consent for [Organization]"** button.

### How to verify:
Check the "Status" column in the API permissions panel. It should show a **Green Checkmark** for all the permissions listed above.

---
*Last Updated: 2026-04-01*
