# Script de prueba para la API de inicio de sesión de QualityDoc
# Ejecutar este script mientras la aplicación .NET está corriendo

$baseUrl = "http://localhost:5113"
$loginUrl = "$baseUrl/api/auth/login"

Write-Host "--- Prueba de API Login QualityDoc ---" -ForegroundColor Cyan

$email = Read-Host "Ingrese su correo"
$password = Read-Host "Ingrese su contraseña" -AsSecureString

# Convertir password de SecureString a texto plano
$ptr = [System.Runtime.InteropServices.Marshal]::SecureStringToBSTR($password)
$plainPassword = [System.Runtime.InteropServices.Marshal]::PtrToStringBSTR($ptr)
[System.Runtime.InteropServices.Marshal]::ZeroFreeBSTR($ptr)

$body = @{
    email = $email
    password = $plainPassword
} | ConvertTo-Json

try {
    Write-Host "Llamando a la API: $loginUrl..." -ForegroundColor Gray
    $response = Invoke-RestMethod -Uri $loginUrl -Method Post -Body $body -ContentType "application/json"
    
    Write-Host "`n¡Inicio de sesión exitoso!" -ForegroundColor Green
    Write-Host "------------------------------------"
    Write-Host "ID:           $($response.id)"
    Write-Host "Nombre:       $($response.nombre)"
    Write-Host "Usuario:      $($response.usuario)"
    Write-Host "Empresa:      $($response.empresa)"
    Write-Host "Rol:          $($response.rol)"
    Write-Host "Departamento: $($response.departamento)"
    Write-Host "------------------------------------"
}
catch {
    $errorMsg = $_.Exception.Message
    if ($_.Exception.Response) {
        $stream = $_.Exception.Response.GetResponseStream()
        $reader = New-Object System.IO.StreamReader($stream)
        $errorMsg = $reader.ReadToEnd()
    }
    Write-Host "`nError al iniciar sesión:" -ForegroundColor Red
    Write-Host $errorMsg
}

Write-Host "`nPresione cualquier tecla para salir..."
$null = [System.Console]::ReadKey($true)
