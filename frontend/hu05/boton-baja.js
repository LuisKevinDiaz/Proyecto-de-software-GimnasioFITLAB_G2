// HU05-T4. Sin estilos, ventana de confirmación ni llamadas HTTP propias.
const conexiones = new WeakMap();

/**
 * Conecta un <button> existente con las funciones de T5 y del backend.
 * Los permisos solo controlan la interfaz; el servidor debe autorizar la baja.
 */
export function conectarBotonBaja(boton, {
    miembroId,
    autorizado = false,
    activo = false,
    confirmarBaja,
    solicitarBaja
} = {}) {
    const TipoBoton = boton?.ownerDocument?.defaultView?.HTMLButtonElement;
    if (!TipoBoton || !(boton instanceof TipoBoton)) {
        throw new TypeError("Se necesita un elemento button.");
    }
    if (!Number.isInteger(miembroId) || miembroId < 1 || miembroId > 2147483647) {
        throw new TypeError("El ID del miembro debe ser un entero positivo válido.");
    }
    if (conexiones.has(boton)) {
        throw new Error("El botón ya tiene una conexión de baja.");
    }

    function validarFuncion(valor) {
        if (valor != null && typeof valor !== "function") {
            throw new TypeError("La integración debe ser una función.");
        }
    }
    validarFuncion(confirmarBaja);
    validarFuncion(solicitarBaja);

    let ocupado = false;
    let destruido = false;
    let revision = 0;
    const opciones = { autorizado, activo, confirmarBaja, solicitarBaja };

    boton.type = "button";
    if (!boton.textContent.trim() && !boton.hasAttribute("aria-label")) {
        boton.textContent = "Dar de baja";
    }

    function disponible() {
        return !destruido && opciones.autorizado === true && opciones.activo === true &&
            typeof opciones.confirmarBaja === "function" &&
            typeof opciones.solicitarBaja === "function";
    }

    function mostrarEstado() {
        boton.hidden = destruido || opciones.autorizado !== true;
        boton.disabled = ocupado || !disponible();
        boton.setAttribute("aria-busy", String(ocupado && !destruido));
    }

    function notificar(tipo, mensaje) {
        if (destruido) return;
        const Evento = boton.ownerDocument.defaultView.CustomEvent;
        boton.dispatchEvent(new Evento("hu05:baja-estado", {
            bubbles: true,
            detail: { miembroId, tipo, mensaje }
        }));
    }

    async function manejarClick() {
        // También protege frente a eventos disparados por código sobre un botón oculto.
        if (ocupado || !disponible()) return;
        ocupado = true;
        const revisionInicial = revision;
        const confirmar = opciones.confirmarBaja;
        const solicitar = opciones.solicitarBaja;
        mostrarEstado();

        function sigueVigente() {
            return revision === revisionInicial && disponible();
        }

        try {
            notificar("confirmando", "Esperando confirmación.");
            if (!sigueVigente()) {
                notificar("cancelado", "La baja no fue solicitada.");
                return;
            }
            const confirmado = await confirmar(miembroId);
            if (destruido) return;
            if (!sigueVigente() || confirmado !== true) {
                notificar("cancelado", "La baja no fue solicitada.");
                return;
            }

            notificar("procesando", "Procesando baja...");
            if (!sigueVigente()) {
                notificar("cancelado", "La baja no fue solicitada.");
                return;
            }
            const respuesta = await solicitar(miembroId);
            if (destruido) return;

            // Contrato del adaptador de interfaz, no del endpoint pendiente de Jeison.
            if (respuesta?.exito === true && respuesta.estado === "inactivo") {
                opciones.activo = false;
                notificar("exito", "Miembro dado de baja correctamente.");
            } else {
                const mensaje = typeof respuesta?.mensaje === "string" && respuesta.mensaje.trim()
                    ? respuesta.mensaje : "El servidor no confirmó la baja.";
                notificar("error", mensaje);
            }
        } catch {
            notificar("error", "No se pudo completar la solicitud de baja.");
        } finally {
            ocupado = false;
            if (!destruido) mostrarEstado();
        }
    }

    boton.addEventListener("click", manejarClick);
    const conexion = {
        actualizar(cambios = {}) {
            if (destruido) throw new Error("La conexión ya fue destruida.");
            const permitidas = ["autorizado", "activo", "confirmarBaja", "solicitarBaja"];
            for (const clave of Object.keys(cambios)) {
                if (!permitidas.includes(clave)) throw new TypeError(`Opción desconocida: ${clave}`);
            }
            for (const clave of ["confirmarBaja", "solicitarBaja"]) {
                if (Object.hasOwn(cambios, clave)) validarFuncion(cambios[clave]);
            }
            if (Object.keys(cambios).some(clave => opciones[clave] !== cambios[clave])) {
                revision++;
                Object.assign(opciones, cambios);
            }
            mostrarEstado();
        },
        destruir() {
            if (destruido) return;
            destruido = true;
            revision++;
            boton.removeEventListener("click", manejarClick);
            conexiones.delete(boton);
            mostrarEstado();
        }
    };
    conexiones.set(boton, conexion);
    mostrarEstado();
    return conexion;
}
