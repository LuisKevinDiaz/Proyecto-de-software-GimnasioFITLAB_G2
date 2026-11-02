#!/usr/bin/env python3
"""Lanzador local. Nunca imprime contraseñas, tokens ni la conexión."""
import datetime,getpass,json,os,pathlib,socket,subprocess,sys,time,urllib.request,urllib.error
repo=pathlib.Path(__file__).resolve().parents[1];work=repo.parent;private=work/'.fitlab-privado';private.mkdir(mode=0o700,exist_ok=True);private.chmod(0o700)
url='http://127.0.0.1:5088';evidence=repo.parents[1]/'outputs/FITLAB-entrega';evidence.mkdir(exist_ok=True)
def env():
    p=private/'conexion.json'
    if not p.exists():raise RuntimeError('Falta la configuración privada de SQL Server; ejecutar preparación local antes de grabar.')
    result=os.environ.copy();result.update(json.loads(p.read_text()));result.update({'ASPNETCORE_URLS':url,'ASPNETCORE_ENVIRONMENT':'Development','Empleados__AutenticacionLocalHabilitada':'true','DOTNET_HOSTBUILDER__RELOADCONFIGONCHANGE':'false','Logging__LogLevel__Microsoft.EntityFrameworkCore':'Warning','FITLAB_API_URL':url});return result
def request(path,body=None,token=None):
    headers={'Content-Type':'application/json'}
    if token:headers['Authorization']='Bearer '+token
    req=urllib.request.Request(url+path,data=None if body is None else json.dumps(body).encode(),headers=headers)
    try:
        with urllib.request.urlopen(req,timeout=10) as r:return r.status,json.load(r)
    except urllib.error.HTTPError as x:return x.code,{}
def session():
    p=private/'sesion.json'
    if not p.exists():raise RuntimeError('Primero ejecuta entrar, en privado y antes de grabar.')
    s=json.loads(p.read_text());code,data=request('/api/sesion/actual',token=s['token'])
    if code!=200:raise RuntimeError('La sesión no es válida o venció. Ejecuta entrar nuevamente, fuera de la grabación.')
    if not data.get('puedeActualizarContacto') or not data.get('puedeDarDeBaja'):raise RuntimeError('La cuenta necesita ambos permisos para mostrar las cinco historias. No se ampliarán automáticamente.')
    return s
command=sys.argv[1]if len(sys.argv)>1 else 'ayuda'
try:
    if command=='api':
        try:
            code,state=request('/api/estado')
            if code==200 and state.get('version')=='entrega-local-v1' and state.get('sqlServer') and state.get('autenticacionLocal'):
                print('La API FITLAB ya está ejecutándose en '+url+' y conectada a FITLAB_Pruebas.');sys.exit(0)
            raise RuntimeError('El puerto está ocupado por otra versión de la API; ciérrala antes de iniciar esta versión.')
        except urllib.error.URLError:pass
        log=(private/'api.log').open('a');process=subprocess.Popen(['dotnet',str(repo/'bin/Release/net9.0/GimnasioFITLAB.dll')],cwd=repo,env=env(),stdout=log,stderr=subprocess.STDOUT)
        try:
            for _ in range(70):
                if process.poll()is not None:raise RuntimeError('La API no inició. Revisar el registro privado api.log; no lo abras mientras grabas.')
                try:
                    code,state=request('/api/estado')
                    if code==200 and state.get('sqlServer') and state.get('autenticacionLocal'):break
                except urllib.error.URLError:pass
                time.sleep(.15)
            else:raise RuntimeError('No se confirmó el inicio de la API ni SQL Server.')
            print('FITLAB listo: '+url+' | SQL Server: FITLAB_Pruebas | acceso de empleados habilitado.',flush=True)
            print('Deja esta terminal abierta. Ctrl+C detiene únicamente esta API.',flush=True);process.wait()
        except KeyboardInterrupt:pass
        finally:
            if process.poll()is None:process.terminate();process.wait(timeout=10)
            log.close()
    elif command in ('crear-cuenta','verificar-clave','cambiar-clave'):
        sys.exit(subprocess.run(['dotnet',str(repo/'tools/EmpleadoPrivado/bin/Release/net9.0/EmpleadoPrivado.dll'),command],env=env(),cwd=repo).returncode)
    elif command=='entrar':
        if not sys.stdin.isatty():raise RuntimeError('Ejecuta entrar en tu terminal privada, sin grabar ni usar tuberías.')
        print('INICIO PRIVADO — no grabes este paso. La contraseña permanecerá oculta.')
        username=input('Usuario: ').strip();password=getpass.getpass('Contraseña: ')
        code,data=request('/api/sesion',{'usuario':username,'clave':password});password=''
        if code!=200:
            diagnostic=private/'ultimo-login.json';diagnostic.write_text(json.dumps({'estadoHttp':code,'momento':datetime.datetime.now(datetime.timezone.utc).isoformat()}));diagnostic.chmod(0o600)
            messages={401:'Usuario o contraseña no válidos. La cuenta se conserva; puedes usar verificar-clave en privado.',429:'Se alcanzó el límite de intentos. Espera un minuto antes de volver a entrar.',400:'La solicitud de acceso no pasó la validación de formato.',404:'La API no ofrece el inicio de sesión esperado; reinicia la versión de entrega.'}
            raise RuntimeError('HTTP '+str(code)+': '+messages.get(code,'La API no pudo completar el acceso; se guardó un diagnóstico sin credenciales.'))
        p=private/'sesion.json';p.write_text(json.dumps({'token':data['accessToken'],'empleadoId':data['empleadoId'],'expiraUtc':data['expiraUtc']}));p.chmod(0o600)
        print('Sesión verificada y guardada de forma privada durante 30 minutos. No se muestra la credencial.')
        session();print('Ambos permisos confirmados. Ya puedes iniciar la grabación.')
    elif command in ('demostrar','seguridad'):
        s=session();e=env();e['FITLAB_ACCESS_TOKEN']=s['token'];e['FITLAB_REQUIRE_HTTP']='1';e['FITLAB_SOLO_HISTORIAS']='0'if '--concurrencia'in sys.argv else '1';e['FITLAB_RESULTADO_JSON']=str(evidence/'ultima-demostracion.json')
        if '--pausas'in sys.argv:e['FITLAB_INTERACTIVO']='1'
        project='Entrega.SqlServer.Verificacion'if command=='demostrar'else'Seguridad.SqlServer.Verificacion'
        dll=repo/'tests'/project/'bin/Release/net9.0'/(project+'.dll')
        if not dll.exists():raise RuntimeError('Falta compilar la verificación antes de grabar.')
        log=evidence/('ultima-demostracion.log'if command=='demostrar'else'seguridad-sqlserver.log')
        with log.open('w') as output:
            process=subprocess.Popen(['dotnet',str(dll)],cwd=repo,env=e,stdout=subprocess.PIPE,stderr=subprocess.STDOUT,text=True)
            for line in process.stdout:
                safe=line.replace(s['token'],'[CREDENCIAL OCULTA]').replace(e['ConnectionStrings__DefaultConnection'],'[CONEXIÓN OCULTA]');output.write(safe);print(safe,end='',flush=True)
            sys.exit(process.wait())
    elif command=='salir':
        p=private/'sesion.json'
        if p.exists():
            saved=json.loads(p.read_text());req=urllib.request.Request(url+'/api/sesion',method='DELETE',headers={'Authorization':'Bearer '+saved['token']})
            try:
                with urllib.request.urlopen(req,timeout=10) as r:pass
            except urllib.error.HTTPError as error:
                if error.code!=401:raise RuntimeError('No se pudo revocar la sesión; no se eliminó el archivo privado.')
            p.unlink()
        print('Sesión cerrada. No se mostraron credenciales.')
    elif command=='datos':
        data=json.loads((evidence/'ultima-demostracion.json').read_text());member=int(data['miembroId'])
        query=f"SET NOCOUNT ON; SELECT DB_NAME() AS BaseActual; SELECT m.Id,m.CodigoUnico,m.Nombre,m.Activo,m.EmpleadoBajaId,m.FechaBaja,mb.Inicio,mb.Vencimiento FROM Miembros m LEFT JOIN Membresias mb ON mb.MiembroId=m.Id WHERE m.Id={member}; SELECT Id,MiembroId,Importe,Moneda,Aplicado FROM Pagos WHERE MiembroId={member}; SELECT MiembroId,EmpleadoId,FechaUtc FROM AuditoriasContacto WHERE MiembroId={member};"
        shell='export SQLCMDPASSWORD="$SA_PASSWORD"; exec /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -C -b -d FITLAB_Pruebas -W -s "|" -i /dev/stdin'
        result=subprocess.run(['docker','exec','-i','sqlserver2022','sh','-c',shell],input=query,text=True,capture_output=True)
        if result.returncode:raise RuntimeError('No se pudo consultar el contenedor. Comprueba Docker; no se muestran datos privados.')
        print(result.stdout)
    elif command=='estado':
        code,state=request('/api/estado');print(json.dumps(state,indent=2,ensure_ascii=False));sys.exit(0 if code==200 and state.get('sqlServer')else 1)
    else:print('Uso: python3 scripts/entrega.py api | crear-cuenta | entrar | estado | datos | demostrar [--pausas] | seguridad | salir')
except (RuntimeError,urllib.error.URLError,KeyError,OSError) as error:
    if isinstance(error,RuntimeError):print(str(error),file=sys.stderr)
    else:print('No se completó el paso. Comprueba que la API y Docker estén funcionando; no se muestran datos privados.',file=sys.stderr)
    sys.exit(1)
