# Database — Driver Compositions

## Migration

`AddDriverCompositions` — tabela `DriverCompositions` + seed WIN.

## Índice

`(TenantId, TargetAsset, DriverAsset)`

## Multi-tenancy

`TenantId` nullable — `null` = composição global da plataforma.

Ver também: [architecture/database.md](architecture/database.md)
