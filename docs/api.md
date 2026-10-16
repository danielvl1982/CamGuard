# API

## 1. GET `/api/members/{id:guid}/images/{imageId:guid}`

- **Verbo y función:** `GET`; entrega en streaming la imagen asociada a un miembro. Feature **F1 — Member management** (`docs/.work/catalog.json`). `src/Enrollment.Web/MemberImageEndpoints.cs:18-28`
- **Autenticación:** el endpoint no declara una política de autorización. El arranque mapea el endpoint y no muestra middleware de autenticación/autorización; esta descripción del acceso efectivo es **inferida del código**. `src/Enrollment.Web/MemberImageEndpoints.cs:18-22`; `src/Enrollment.Web/Program.cs:16-35`
- **Petición:** sin cuerpo, parámetros de consulta ni cabeceras de aplicación. La ruta recibe `id` e `imageId` como `Guid`; ambos deben ser distintos de `Guid.Empty`, rechazado por sus tipos de dominio. `src/Enrollment.Web/MemberImageEndpoints.cs:22-34`; `src/Enrollment.Domain/MemberId.cs:17-21`; `src/Enrollment.Domain/MemberImageId.cs:17-21`
- **Respuestas:** `200 OK` (respuesta de archivo predeterminada) con los bytes del archivo encontrado; `404 Not Found` si un ID está vacío, no existe el miembro o la imagen, el nombre/ruta almacenados no son seguros o falta el archivo. `src/Enrollment.Web/MemberImageEndpoints.cs:30-68`
- **Cuerpo de éxito:** contenido binario de imagen, no un DTO JSON. El MIME se deriva de la extensión: `.jpg`/`.jpeg` → `image/jpeg`, `.png` → `image/png`, `.webp` → `image/webp`, `.gif` → `image/gif`, `.bmp` → `image/bmp`; otras extensiones → `application/octet-stream`. `src/Enrollment.Web/MemberImageEndpoints.cs:68-80`
- **Ejemplo:** guardar la imagen en un archivo; reemplazar las variables por la URL base y los GUID existentes:

  ```bash
  curl --fail --show-error "$BASE_URL/api/members/$MEMBER_ID/images/$IMAGE_ID" --output member-image
  ```

## 2. Errores

El endpoint usa `404 Not Found` sin un cuerpo de error definido por la aplicación; no implementa `ProblemDetails` para esta ruta. `src/Enrollment.Web/MemberImageEndpoints.cs:30-68`

## 3. Documentación interactiva

**No aplica:** el arranque configura Razor Components y mapea el endpoint, sin mostrar una ruta Swagger/OpenAPI. `src/Enrollment.Web/Program.cs:5-13,28-35`

## 4. Contratos de datos

La petición contiene dos GUID en la ruta. La respuesta exitosa es el archivo de imagen y su MIME se determina a partir de la extensión; no hay DTO JSON en este contrato. `src/Enrollment.Web/MemberImageEndpoints.cs:22-28,68-80`
