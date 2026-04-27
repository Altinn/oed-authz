# oed-authz
ASP.NET Core Web API handling events for OED/DD roles, persisting them and providing av PIP API for Altinn Authorization. 
This also exposes an API for external consumers requiring Maskinporten-authentication.

See [https://digitaltdodsbo.tt02.altinn.no/swagger](https://digitaltdodsbo.tt02.altinn.no/swagger) for API documentation.

## Using the API for external consumers (banks etc.)
External consumers should use the endpoint `/api/v1/authorization/roles/search`. This endpoint requires a Maskinporten-token with the scope `altinn:dd:authlookup`. The response will contain any court assigned roles and user assigned proxy roles. The following role codes will be made available:
<p align="center">
    <img width="500" alt="oed_authz_role_types" src="https://github.com/user-attachments/assets/0a30bad3-1711-41ad-8410-417f878c152d" />
</p>


### Court assigned roles
| Role code | Description
| :--- | :--- |
| `urn:domstolene:digitaltdodsbo:formuesfullmakt` | An heir with a power of attorney over assets has the right to insight into the values of an estate. <br /><br />After a probate certificate is issued this role could be is somewhat restricted based on business rules. In these cases the additional property `isRestricted` will be `true`. |
| `urn:domstolene:digitaltdodsbo:skifteattest` | An heir who has assumed liability for the debts of an estate can act on behalf of the estate together with any other heirs who have assumed debt liability |

### Proxy roles

Within an estate, heirs with a probate certificate can assign proxies that may act on their behalf. These roles are not
assigned by the court, but by the heirs themselves.

| Role code | Description
| :--- | :--- |
| `urn:altinn:digitaltdodsbo:skiftefullmakt:individuell` | An heir with an individual power of attorney over assets has been granted the right to act on behalf of another heir in the estate who has assumed debt liability for the estate. |
| `urn:altinn:digitaltdodsbo:skiftefullmakt:kollektiv` | An heir who has received a power of attorney for the settlement of the estate from all the other heirs in the estate can act on behalf of the estate alone. |

Note that the `kollektiv` role is assigned if and only if all heirs with a probate certificate have appointed the same 
proxy. Thus, for a recipient to receive the `kollektiv` role, the response will also contain a `individuell` role for all 
heirs with a probate certificate to that same recipient (unless that recipient also has a probate certificate; there is 
no need to assign a proxy role to oneself).  If at any point any of the heirs with a probate certificate revokes their 
`individuell` role, the `kollektiv` role will also be revoked.

> [!WARNING]
> The roles can change at any time, so values returned from the endpoint must not be cached by the consumers.

### Examples

Requests must contain a `Authorization`-header with a Maskinporten-token using the `Bearer` scheme. The request body 
must be a JSON object with `estateSsn`, which must be 11-digit norwegian identification numbers. 


#### Scenario 1
In this scenario there is only one heir to the estate. The probate certificate has been isued to the single heir and the single heir can act on behalf of the estate alone.
<p align="center">
    <img width="500" alt="oed_authz_scenario_1_sole_heir" src="https://github.com/user-attachments/assets/6d444e8b-d7d9-46dd-9871-73c5019756f3" />
</p>




```jsonc
// POST https://digitaltdodsbo.tt02.altinn.no/api/v1/authorization/roles/search
{
    "estateSsn": "01827974788"
}
```

Response:
```jsonc
{
  "roleAssignments": [
    {
      "estateSsn": "01827974788",
      "recipientSsn": "28857697520",
      "role": "urn:altinn:digitaltdodsbo:skiftefullmakt:kollektiv",
      "created": "2026-03-19T13:19:04.625861+00:00",
      "isRestricted": false
    },
    {
      "estateSsn": "01827974788",
      "recipientSsn": "28857697520",
      "role": "urn:domstolene:digitaltdodsbo:skifteattest",
      "created": "2026-03-19T13:18:30.83395+00:00",
      "isRestricted": false
    }
  ]
}
```

#### Scenario 2
In this scenario there are three heirs to the estate. Probate certificate has been issued to all three heirs. The heirs can act on behalf of the estate together with the other heirs, but not alone.

<p align="center">
    <img width="500" alt="oed_authz_scenario_2_no_delegation" src="https://github.com/user-attachments/assets/75a12d8f-0410-41e2-9bf1-05133288b41f" />
</p>

```jsonc
// POST https://digitaltdodsbo.tt02.altinn.no/api/v1/authorization/roles/search
{
    "estateSsn": "18855699938"
}
```

Response:
```jsonc
{
  "roleAssignments": [

    {
      "estateSsn": "18855699938",
      "recipientSsn": "20856099858",
      "role": "urn:domstolene:digitaltdodsbo:skifteattest",
      "created": "2026-03-19T13:16:50.571733+00:00",
      "isRestricted": false
    },
    {
      "estateSsn": "18855699938",
      "recipientSsn": "28857697520",
      "role": "urn:domstolene:digitaltdodsbo:skifteattest",
      "created": "2026-03-19T13:16:50.571733+00:00",
      "isRestricted": false
    },
    {
      "estateSsn": "18855699938",
      "recipientSsn": "24848299983",
      "role": "urn:domstolene:digitaltdodsbo:skifteattest",
      "created": "2026-03-19T13:16:50.571733+00:00",
      "isRestricted": false
    }
  ]
}
```

#### Scenario 3
In this scenario there are three heirs to the estate. Probate certificate has been issued to all three heirs, but two of the heirs has given the power of attorney proxy role to the third heir. The third heir has therefore the right to act on behalf of the estate alone.

<p align="center">
    <img width="500" alt="oed_authz_scenario_3_full_delegation" src="https://github.com/user-attachments/assets/1d526653-2455-4eda-a414-25c38319e1cb" />
</p>

```jsonc
// POST https://digitaltdodsbo.tt02.altinn.no/api/v1/authorization/roles/search
{
    "estateSsn": "18855699938"
}
```

Response:
```jsonc
{
  "roleAssignments": [
    {
      "estateSsn": "18855699938",
      "recipientSsn": "20856099858",
      "role": "urn:domstolene:digitaltdodsbo:skifteattest",
      "created": "2026-03-19T13:16:50.571733+00:00",
      "isRestricted": false
    },
    {
      "estateSsn": "18855699938",
      "recipientSsn": "28857697520",
      "role": "urn:domstolene:digitaltdodsbo:skifteattest",
      "created": "2026-03-19T13:16:50.571733+00:00",
      "isRestricted": false
    },
    {
      "estateSsn": "18855699938",
      "recipientSsn": "24848299983",
      "role": "urn:domstolene:digitaltdodsbo:skifteattest",
      "created": "2026-03-19T13:16:50.571733+00:00",
      "isRestricted": false
    },
    {
      "estateSsn": "18855699938",
      "recipientSsn": "24848299983",
      "heirSsn": "20856099858",
      "role": "urn:altinn:digitaltdodsbo:skiftefullmakt:individuell",
      "created": "2026-03-19T14:45:11.245322+00:00",
      "isRestricted": false
    },
    {
      "estateSsn": "18855699938",
      "recipientSsn": "24848299983",
      "heirSsn": "28857697520",
      "role": "urn:altinn:digitaltdodsbo:skiftefullmakt:individuell",
      "created": "2026-03-19T14:45:11.245322+00:00",
      "isRestricted": false
    },
    {
      "estateSsn": "18855699938",
      "recipientSsn": "24848299983",
      "role": "urn:altinn:digitaltdodsbo:skiftefullmakt:kollektiv",
      "created": "2026-03-19T14:45:11.245322+00:00",
      "isRestricted": false
    },
  ]
}
```

#### Scenario 4
In this scenario there are three heirs to the estate. Probate certificate has been issued to all three heirs. Heir one has given the power of attorney proxy role to the third heir, but the second heir has not. The third heir can act on behalf of the first heir, an therefore on behalf of the estate together with the second heir.

<p align="center">
    <img width="500" alt="oed_authz_scenario_4_partial_delegation" src="https://github.com/user-attachments/assets/103cec01-9038-48d8-bffb-5fb17b9b28ed" />
</p>

```jsonc
// POST https://digitaltdodsbo.tt02.altinn.no/api/v1/authorization/roles/search
{
    "estateSsn": "18855699938"
}
```

Response:
```jsonc
{
  "roleAssignments": [
    {
      "estateSsn": "18855699938",
      "recipientSsn": "20856099858",
      "role": "urn:domstolene:digitaltdodsbo:skifteattest",
      "created": "2026-03-19T13:16:50.571733+00:00",
      "isRestricted": false
    },
    {
      "estateSsn": "18855699938",
      "recipientSsn": "28857697520",
      "role": "urn:domstolene:digitaltdodsbo:skifteattest",
      "created": "2026-03-19T13:16:50.571733+00:00",
      "isRestricted": false
    },
    {
      "estateSsn": "18855699938",
      "recipientSsn": "24848299983",
      "role": "urn:domstolene:digitaltdodsbo:skifteattest",
      "created": "2026-03-19T13:16:50.571733+00:00",
      "isRestricted": false
    },
    {
      "estateSsn": "18855699938",
      "recipientSsn": "24848299983",
      "heirSsn": "20856099858",
      "role": "urn:altinn:digitaltdodsbo:skiftefullmakt:individuell",
      "created": "2026-03-19T14:45:11.245322+00:00",
      "isRestricted": false
    }
  ]
}
```

## Internal Altinn usage 

### PIP API

This API is meant for Altinn Authorization to use as a PIP (Policy Information Point) extension for the context handler to 
retrieve roles when a given policy refers to roles of type `urn:digitaltdodsbo:rolecode`.

Supply a `PipRequest`-body with one or both the `from` and `to` properties set to norwegian identification numbers for the deceased 
(estate) and heir (recipient), respectively to the endpoint `/api/v1/pip`. One of the parameters can be omitted to get a list of 
all relations for the given from/to. This will include additional roles compared to the API for external consumers, and will
also include `urn:altinn:digitaltdodsbo:skiftefullmakt:kollektiv` (but not `urn:altinn:digitaltdodsbo:skiftefullmakt:individuell`
as this assignment is within the context of a single estate).

This requires a Maskinporten-token with the scope `altinn:dd:internal`

#### Example

```jsonc
// POST https://digitaltdodsbo.tt02.altinn.no/api/v1/pip
{
    "from": "11111111111",
    // "to": "22222222211" // Only one of "from" and "to" is required
}
```

Response:
```jsonc
{
    "roleAssignments": [
        {
            "urn:digitaltdodsbo:rolecode": "urn:domstolene:digitaltdodsbo:formuesfullmakt",
            "from": "11111111111",
            "to": "22222222211",
            "created": "2023-02-20T10:00:06.401416+00:00"
        },
        {
            "urn:digitaltdodsbo:rolecode": "urn:domstolene:digitaltdodsbo:arving:ektefelleEllerPartner",
            "from": "11111111111",
            "to": "22222222211",
            "created": "2023-02-20T10:00:06.401416+00:00"
        },
        {
            "urn:digitaltdodsbo:rolecode": "urn:domstolene:digitaltdodsbo:skifteattest",
            "from": "11111111111",
            "to": "22222222211",
            "created": "2023-02-20T10:00:06.401416+00:00"
        },
        // ... some rows omitted for brevity
        {
            "urn:digitaltdodsbo:rolecode": "urn:altinn:digitaltdodsbo:skiftefullmakt:kollektiv",
            "from": "11111111111",
            "to": "44444444411",
            "created": "2023-02-20T10:00:06.401416+00:00"
        }
    ]
}
```

## DD proxies administration API

There's an RPC API for managing `urn:altinn:digitaltdodsbo:skiftefullmakt` roles for internal consumers only. 
This is used by Digitat Dødsbo to grant and revoke roles for proxies. 

This endpoint requires a Maskinporten-token with the scope `altinn:dd:internal`. Only roles within the 
`urn:altinn:digitaltdodsbo:skiftefullmakt` namespace can be managed.

### Getting assignments

See the external proxy API for getting a list of assignments. The `altinn:dd:internal` scope is also authorized for that
endpoint.

### Adding an assignment

Post the body below to the `add` endpoint. `created` can be omitted, and will be set to the current time if omitted.

```jsonc
// POST https://digitaltdodsbo.tt02.altinn.no/api/v1/authorization/proxies/add
{
    "add": {
        "estateSsn": "11111111111"
        "heirSsn": "22222222211",
        "recipientSsn": "44444444411",
        "urn:digitaltdodsbo:rolecode": "urn:altinn:digitaltdodsbo:skiftefullmakt:individuell",
        "created": "2023-02-20T10:00:06.401416+00:00"
    }
}
// Response: 201 Created, with the estate with all current proxy assignments (as with /proxies/search)
```

### Deleting an assignment

Post the body below to the `remove` endpoint. 

```http
// POST https://digitaltdodsbo.tt02.altinn.no/api/v1/authorization/proxies/remove
{
    "remove": {
        "estateSsn": "11111111111"
        "heirSsn": "22222222211",
        "recipientSsn": "44444444411",
        "urn:digitaltdodsbo:rolecode": "urn:altinn:digitaltdodsbo:skiftefullmakt:individuell"
    }
}
 Response: 204 No Content 
```

## Local development setup

1. Install PostgreSQL 13 or later
2. Install pgAdmin
4. Create a database locally with name `oedauthz`
3. Create the user `oedpgadmin` (only used for migrations), set password to `secret`. Give all privileges to `oedauthz`
4. Create the user `oedpguser`, set password to `secret`. Give usage privileges to `oedauthz`.
5. Run/debug the project

This should build and migrate the database. Open https://localhost/swagger for Swagger UI.
