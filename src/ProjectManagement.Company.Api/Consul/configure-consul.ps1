﻿$token = "13585e24-52a3-e906-981a-b2246ed54d77"

consul acl policy create -name="kv-company-api" -description="Policy that grants KV access to company-api" -rules @company-api-policies.hcl -token="$token"
consul acl token create -policy-name="kv-company-api" -service-identity="company-api" -token="$token"

consul kv put -token="$token" company-api/app-config @app-config.json