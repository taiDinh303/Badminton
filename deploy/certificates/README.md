# Supabase database CA

Public CA certificate downloaded from the URL used by Supabase's official dashboard:
https://supabase-downloads.s3-ap-southeast-1.amazonaws.com/prod/ssl/prod-ca-2021.crt

SHA-256: `700723581420DD1AC98FD7E9AC529F0EF210EADCAF87FC868A3AD7D114C2F3B7`.

This is a public trust certificate, not a private key or account credential. Npgsql uses it through `Root Certificate=/app/certificates/supabase-ca.crt;SSL Mode=VerifyFull`. Do not disable TLS certificate/hostname verification to resolve connection errors.
