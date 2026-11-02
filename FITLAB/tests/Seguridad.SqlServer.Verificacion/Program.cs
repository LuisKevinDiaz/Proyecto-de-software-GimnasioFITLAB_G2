using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using GimnasioFITLAB.Data;
using GimnasioFITLAB.Models;
using GimnasioFITLAB.Security;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

var connection=Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection");
var token=Environment.GetEnvironmentVariable("FITLAB_ACCESS_TOKEN");
if(string.IsNullOrWhiteSpace(connection)||string.IsNullOrWhiteSpace(token)) {Console.Error.WriteLine("Falta la conexión privada o una sesión real. No se crean cuentas de prueba.");return 1;}
var sql=new SqlConnectionStringBuilder(connection);
if(sql.InitialCatalog!="FITLAB_Pruebas"||sql.DataSource.Split(',')[0]!="127.0.0.1") {Console.Error.WriteLine("Solo se admite FITLAB_Pruebas local.");return 1;}
var address=new Uri(Environment.GetEnvironmentVariable("FITLAB_API_URL")??"http://127.0.0.1:5088");
if(!address.IsLoopback){Console.Error.WriteLine("Solo se admite API local.");return 1;}
AppDbContext Db()=>new(new DbContextOptionsBuilder<AppDbContext>().UseSqlServer(connection).Options);
using var client=new HttpClient(new HttpClientHandler{UseCookies=false,AllowAutoRedirect=false}){BaseAddress=address};
async Task<HttpResponseMessage> Call(HttpMethod method,string path,object? body=null,string? bearer=null,string? etag=null)
{
 using var request=new HttpRequestMessage(method,path);
 if(body!=null)request.Content=JsonContent.Create(body);
 if(bearer!=null)request.Headers.Authorization=new AuthenticationHeaderValue("Bearer",bearer);
 if(etag!=null)request.Headers.TryAddWithoutValidation("If-Match",etag);
 return await client.SendAsync(request);
}
int count=0;void Check(bool ok,string text){if(!ok)throw new InvalidOperationException(text);Console.WriteLine("OK SEGURIDAD: "+text);count++;}
int? actor=null;bool originalActivo=false,originalContacto=false,originalBaja=false;DateTimeOffset originalExpiry=default;
try
{
 var hash=TokensSesion.Hash(token);
 await using(var db=Db())
 {
  var session=await db.SesionesEmpleados.AsNoTracking().Include(s=>s.Empleado).SingleAsync(s=>s.TokenHash==hash);
  actor=session.EmpleadoId;originalActivo=session.Empleado.Activo;originalContacto=session.Empleado.PuedeActualizarContacto;originalBaja=session.Empleado.PuedeDarDeBaja;originalExpiry=session.ExpiraUtc;
  Check(session.TokenHash!=token&&session.TokenHash==Convert.ToHexString(SHA256.HashData(Convert.FromHexString(token))),"La sesión SQL guarda SHA256, no el token original");
  Check(session.Empleado.PasswordHash.Length>60&&session.Empleado.PasswordHash.StartsWith("AQAAAA"),"Cuenta persistida con hash IdentityV3, sin contraseña legible");
 }
 Check((await Call(HttpMethod.Get,"/api/sesion/actual",bearer:token)).StatusCode==HttpStatusCode.OK,"Sesión emitida tras login real aceptada: HTTP 200");
 Check((await Call(HttpMethod.Get,"/api/sesion/actual")).StatusCode==HttpStatusCode.Unauthorized,"Sin sesión: HTTP 401");
 Check((await Call(HttpMethod.Get,"/api/sesion/actual",bearer:new string('0',64))).StatusCode==HttpStatusCode.Unauthorized,"Token no registrado: HTTP 401");
 var registration=new {nombre="PRUEBA SEGURIDAD "+Guid.NewGuid().ToString("N")[..8],cedula="999"+RandomNumberGenerator.GetInt32(100000000).ToString("D8"),telefono="8095550101",correo="seguridad@example.invalid",empleadoRegistroId=999};
 Check((await Call(HttpMethod.Post,"/api/miembros/registro",registration)).StatusCode==HttpStatusCode.Unauthorized,"Registro sin sesión rechazado: HTTP 401");
 var created=await Call(HttpMethod.Post,"/api/miembros/registro",registration,token);
 Check(created.StatusCode==HttpStatusCode.Created,"Registro autenticado permitido: HTTP 201");
 var member=(await created.Content.ReadFromJsonAsync<Miembro>())!;
 Check(member.EmpleadoRegistroId==actor,"Alta atribuida al empleado de la sesión, ignorando el ID declarado");
 var profile=await client.GetAsync($"/api/miembros/{member.Id}");var etag=profile.Headers.ETag!.Tag;
 var contact=new {telefono="8295550102",correo="actualizado-seguridad@example.invalid"};
 Check((await Call(HttpMethod.Post,$"/api/miembros/{member.Id}/baja",new {empleadoId=1})).StatusCode==HttpStatusCode.Unauthorized,"Declarar ID1 sin sesión sigue rechazado: HTTP 401");
 Check((await Call(HttpMethod.Post,$"/api/miembros/{member.Id}/baja",new {empleadoId=actor},token)).StatusCode==HttpStatusCode.BadRequest,"Sesión válida no permite elegir empleado en el cuerpo: HTTP 400");
 await using(var db=Db()){var e=await db.EmpleadosCuentas.FindAsync(actor);e!.PuedeActualizarContacto=false;e.PuedeDarDeBaja=false;await db.SaveChangesAsync();}
 Check((await Call(HttpMethod.Get,"/api/sesion/actual",bearer:token)).StatusCode==HttpStatusCode.OK,"Sin permisos sigue autenticado: HTTP 200");
 Check((await Call(HttpMethod.Put,$"/api/miembros/{member.Id}/contacto",contact,token,etag)).StatusCode==HttpStatusCode.Forbidden,"Empleado sin permiso de contacto: HTTP 403");
 Check((await Call(HttpMethod.Post,$"/api/miembros/{member.Id}/baja",new {},token)).StatusCode==HttpStatusCode.Forbidden,"Empleado sin permiso de baja: HTTP 403");
 await using(var db=Db()){var e=await db.EmpleadosCuentas.FindAsync(actor);e!.Activo=false;await db.SaveChangesAsync();}
 Check((await Call(HttpMethod.Get,"/api/sesion/actual",bearer:token)).StatusCode==HttpStatusCode.Unauthorized,"Cuenta desactivada invalida sesión: HTTP 401");
 await using(var db=Db()){var e=await db.EmpleadosCuentas.FindAsync(actor);e!.Activo=originalActivo;e.PuedeActualizarContacto=originalContacto;e.PuedeDarDeBaja=originalBaja;var ss=await db.SesionesEmpleados.FindAsync(hash);ss!.ExpiraUtc=DateTimeOffset.UtcNow.AddMinutes(-1);await db.SaveChangesAsync();}
 Check((await Call(HttpMethod.Get,"/api/sesion/actual",bearer:token)).StatusCode==HttpStatusCode.Unauthorized,"Sesión vencida rechazada: HTTP 401");
 await using(var db=Db()){var ss=await db.SesionesEmpleados.FindAsync(hash);ss!.ExpiraUtc=originalExpiry;await db.SaveChangesAsync();}
 Check((await Call(HttpMethod.Put,$"/api/miembros/{member.Id}/contacto",contact,token,etag)).StatusCode==HttpStatusCode.NoContent,"Permiso restaurado permite contacto: HTTP 204");
 Check((await Call(HttpMethod.Post,$"/api/miembros/{member.Id}/baja",new {},token)).StatusCode==HttpStatusCode.OK,"Permiso restaurado permite baja: HTTP 200");
 await using(var db=Db()){var m=await db.Miembros.FindAsync(member.Id);var audit=await db.AuditoriasContacto.SingleAsync(a=>a.MiembroId==member.Id);Check(m!.EmpleadoBajaId==actor&&audit.EmpleadoId==actor,"Ambas auditorías identifican al empleado validado en SQL Server");}
 Console.WriteLine($"RESULTADO SEGURIDAD: {count} comprobaciones reales aprobadas; permisos y estado originales restaurados.");return 0;
}
catch(Exception){Console.Error.WriteLine("SEGURIDAD NO COMPLETADA. Se restaurarán los permisos; revisar diagnóstico técnico sin mostrar credenciales.");return 1;}
finally
{
 if(actor!=null){await using var db=Db();var e=await db.EmpleadosCuentas.FindAsync(actor);if(e!=null){e.Activo=originalActivo;e.PuedeActualizarContacto=originalContacto;e.PuedeDarDeBaja=originalBaja;}var session=await db.SesionesEmpleados.FindAsync(TokensSesion.Hash(token));if(session!=null)session.ExpiraUtc=originalExpiry;await db.SaveChangesAsync();}
}
