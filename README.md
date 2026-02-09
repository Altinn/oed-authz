# oed-authz
ASP.NET Core Web API handling events for OED/DD roles, persisting them and providing av PIP API for Altinn Authorization. 
This also exposes an API for external consumers requiring Maskinporten-authentication.

See [https://digitaltdodsbo.tt02.altinn.no/swagger](https://digitaltdodsbo.tt02.altinn.no/swagger) for API documentation.

## Using the API for external consumers (banks etc.)

There are two API endpoints; one for retrieving court assigned roles for a given estate, and one for retrieving proxy roles
assigned from the heirs to others within the estate.

### Court assigned roles

For court assigned roles use `/api/v1/authorization/roles/search`. This endpoint requires a Maskinporten-token with the scope; 
`altinn:dd:authlookup`. The following role codes will be made available

* `urn:domstolene:digitaltdodsbo:formuesfullmakt` 
* `urn:domstolene:digitaltdodsbo:skifteattest` 

#### Example

Requests must contain a `Authorization`-header with a Maskinporten-token using the `Bearer` scheme. The request body 
must be a JSON object with `estateSsn`, or `recipientSsn`, or both, which must be 11-digit norwegian identification numbers. 

```jsonc
// POST https://digitaltdodsbo.tt02.altinn.no/api/v1/authorization/roles/search
{
    "estateSsn": "11111111111"
    // "recipientSsn": "22222222211" // Only one of "estateSsn" and "recipientSsn" is required
}
```

Response:
```jsonc
{    
    "roleAssignments": [
        {
            "estateSsn": "11111111111",
            "recipientSsn": "22222222211",
            "role": "urn:domstolene:digitaltdodsbo:skifteattest",
            "created": "2023-02-20T10:00:06.401416+00:00"
        },
        {
            "estateSsn": "11111111111",
            "recipientSsn": "22222222211",
            "role": "urn:domstolene:digitaltdodsbo:skifteattest",
            "created": "2023-02-20T10:00:06.401416+00:00"
        }
    ]
}
```
### Proxy roles

Within an estate, heirs with a probate certificate can assign proxies that may act on their behalf. These roles are not
assigned by the court, but by the heirs themselves. To retrieve these proxy roles, use the endpoint `/api/v1/authorization/proxies/search`.

The following role codes are currently available:

* `urn:altinn:digitaltdodsbo:skiftefullmakt:individuell` (granted to a specific heir from a specific heir)
* `urn:altinn:digitaltdodsbo:skiftefullmakt:kollektiv` (granted to a specific heir from all heirs)

Note that the `kollektiv` role is assigned if and only if all heirs with a probate certificate have appointed the same 
proxy. Thus, for a recipient to receive the `kollektiv` role, the response will also contain a `individuell` role for all 
heirs with a probate certificate to that same recipient (unless that recipient also has a probate certificate; there is 
no need to assign a proxy role to oneself).  If at any point any of the heirs with a probate certificate revokes their 
`individuell` role, the `kollektiv` role will also be revoked.

If no relation (ie. role assignment) exists, an empty `roleAssignments` array will be returned.

#### Example

Requests must contain a `Authorization`-header with a Maskinporten-token using the `Bearer` scheme. The request body
must be a JSON object with `estateSsn`, or `recipientSsn`, or both, which must be 11-digit norwegian identification numbers. 

```jsonc
// POST https://digitaltdodsbo.tt02.altinn.no/api/v1/authorization/proxies/search
{
    "estateSsn": "11111111111" // this estate has two heirs with probate certificates; 22222222211 and 33333333311
}
```

Response:
```jsonc
{    
    "proxyAssignments": [
        {
            "estateSsn": "11111111111",
            "heirSsn": null, // Assigned from the estate itself (ie no particular heir); can act on behalf of all heirs  
            "recipientSsn": "44444444411",
            "role": "urn:altinn:digitaltdodsbo:skiftefullmakt:kollektiv",
            "created": "2023-02-20T10:00:06.401416+00:00"
        },
        {
            // Assigned from the individual heir; can act on behalf of that heir
            "estateSsn": "11111111111",
            "heirSsn": "22222222211",
            "recipientSsn": "44444444411",
            "role": "urn:altinn:digitaltdodsbo:skiftefullmakt:individuell",
            "created": "2023-02-20T10:00:06.401416+00:00"
        },
        {
            // Assigned from the individual heir; can act on behalf of that heir
            "estateSsn": "11111111111",
            "heirSsn": "33333333311",
            "recipientSsn": "44444444411",
            "role": "urn:altinn:digitaltdodsbo:skiftefullmakt:individuell",
            "created": "2023-02-20T10:00:06.401416+00:00"
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
    }
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
