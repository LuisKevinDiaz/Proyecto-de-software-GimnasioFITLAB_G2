import { conectarBotonBaja } from "../../frontend/hu05/boton-baja.js";

const casos = [];
const prueba = (nombre, ejecutar) => casos.push({ nombre, ejecutar });
const igual = (real, esperado, mensaje = "Valor inesperado") => {
    if (real !== esperado) throw new Error(`${mensaje}: ${real} !== ${esperado}`);
};
const pausa = () => new Promise(resolve => setTimeout(resolve, 0));
function pendiente() {
    let resolver;
    const promesa = new Promise(resolve => { resolver = resolve; });
    return { promesa, resolver };
}

const escenarios = [];
function escenario(opciones = {}, contenido = "Dar de baja") {
    const boton = document.createElement("button");
    boton.textContent = contenido;
    document.querySelector("#escenario").append(boton);
    const eventos = [];
    const llamadas = { confirmacion: 0, baja: 0 };
    boton.addEventListener("hu05:baja-estado", e => eventos.push(e.detail));
    const conexion = conectarBotonBaja(boton, {
        miembroId: 7,
        autorizado: true,
        activo: true,
        confirmarBaja: async id => { igual(id, 7); llamadas.confirmacion++; return true; },
        solicitarBaja: async id => { igual(id, 7); llamadas.baja++; return { exito: true, estado: "inactivo" }; },
        ...opciones
    });
    const resultado = { boton, conexion, eventos, llamadas };
    escenarios.push(resultado);
    return resultado;
}

function terminar(boton, tipo) {
    return new Promise((resolve, reject) => {
        const limite = setTimeout(() => {
            boton.removeEventListener("hu05:baja-estado", recibir);
            reject(new Error(`No se recibió el evento ${tipo}`));
        }, 1500);
        function recibir(e) {
            if (e.detail.tipo !== tipo) return;
            clearTimeout(limite);
            boton.removeEventListener("hu05:baja-estado", recibir);
            resolve(e.detail);
        }
        boton.addEventListener("hu05:baja-estado", recibir);
    });
}

prueba("Permiso ausente: oculto y sin acciones", async () => {
    const e = escenario({ autorizado: undefined });
    igual(e.boton.hidden, true);
    igual(e.boton.disabled, true);
    e.boton.dispatchEvent(new MouseEvent("click"));
    await pausa();
    igual(e.llamadas.confirmacion, 0);
    igual(e.llamadas.baja, 0);
});

for (const valor of [false, null, "true", 1]) {
    prueba(`El permiso ${JSON.stringify(valor)} no equivale a autorización explícita`, () => {
        const e = escenario({ autorizado: valor });
        igual(e.boton.hidden, true);
        igual(e.boton.disabled, true);
    });
}

prueba("Personal autorizado: botón visible, accesible por teclado y de tipo button", () => {
    const e = escenario();
    igual(e.boton.hidden, false);
    igual(e.boton.disabled, false);
    igual(e.boton.type, "button");
    e.boton.focus();
    igual(document.activeElement, e.boton);
});

prueba("Conserva el contenido y las clases proporcionados por el diseño", () => {
    const e = escenario({}, "Dar de baja al miembro");
    e.boton.className = "accion-diseno";
    e.conexion.actualizar({ autorizado: false });
    e.conexion.actualizar({ autorizado: true });
    igual(e.boton.textContent, "Dar de baja al miembro");
    igual(e.boton.className, "accion-diseno");
});

prueba("Un botón vacío recibe un nombre comprensible", () => {
    igual(escenario({}, "").boton.textContent, "Dar de baja");
});

prueba("Un miembro inactivo o de estado desconocido no se puede dar de baja", async () => {
    for (const activo of [false, undefined, "true"]) {
        const e = escenario({ activo });
        igual(e.boton.disabled, true);
        e.boton.dispatchEvent(new MouseEvent("click"));
        await pausa();
        igual(e.llamadas.confirmacion, 0);
    }
});

prueba("Sin conexión a T5 o al backend permanece deshabilitado", () => {
    igual(escenario({ confirmarBaja: undefined }).boton.disabled, true);
    igual(escenario({ solicitarBaja: undefined }).boton.disabled, true);
});

prueba("Permite conectar los adaptadores posteriormente", async () => {
    const e = escenario({ confirmarBaja: undefined, solicitarBaja: undefined });
    e.conexion.actualizar({
        confirmarBaja: async () => true,
        solicitarBaja: async () => ({ exito: true, estado: "inactivo" })
    });
    igual(e.boton.disabled, false);
    const fin = terminar(e.boton, "exito");
    e.boton.click();
    await fin;
    igual(e.boton.disabled, true);
});

prueba("Cancelar la confirmación no llama al backend ni desactiva al miembro", async () => {
    const e = escenario({ confirmarBaja: async () => false });
    const fin = terminar(e.boton, "cancelado");
    e.boton.click();
    await fin;
    igual(e.llamadas.baja, 0);
    igual(e.boton.disabled, false);
});

prueba("Una confirmación ambigua no autoriza la solicitud", async () => {
    const e = escenario({ confirmarBaja: async () => "true" });
    const fin = terminar(e.boton, "cancelado");
    e.boton.click();
    await fin;
    igual(e.llamadas.baja, 0);
});

prueba("Solo un éxito confirmado cambia el estado visual", async () => {
    const respuesta = pendiente();
    const e = escenario({ solicitarBaja: () => respuesta.promesa });
    const fin = terminar(e.boton, "exito");
    e.boton.click();
    await pausa();
    igual(e.eventos.some(v => v.tipo === "exito"), false);
    igual(e.boton.getAttribute("aria-busy"), "true");
    respuesta.resolver({ exito: true, estado: "inactivo" });
    const evento = await fin;
    igual(evento.miembroId, 7);
    igual(e.boton.disabled, true);
    igual(e.boton.getAttribute("aria-busy"), "false");
});

prueba("Un rechazo mantiene al miembro activo y permite reintentar", async () => {
    const e = escenario({ solicitarBaja: async () => ({ exito: false, mensaje: "Operación rechazada." }) });
    let fin = terminar(e.boton, "error");
    e.boton.click();
    igual((await fin).mensaje, "Operación rechazada.");
    igual(e.boton.disabled, false);
    e.conexion.actualizar({ solicitarBaja: async () => ({ exito: true, estado: "inactivo" }) });
    fin = terminar(e.boton, "exito");
    e.boton.click();
    await fin;
    igual(e.boton.disabled, true);
});

for (const etapa of ["confirmarBaja", "solicitarBaja"]) {
    prueba(`Maneja excepciones en ${etapa} sin anunciar una baja`, async () => {
        const e = escenario({ [etapa]: async () => { throw new Error("Fallo simulado"); } });
        const fin = terminar(e.boton, "error");
        e.boton.click();
        await fin;
        igual(e.eventos.some(v => v.tipo === "exito"), false);
        igual(e.boton.disabled, false);
        igual(e.boton.getAttribute("aria-busy"), "false");
        if (etapa === "confirmarBaja") igual(e.llamadas.baja, 0);
    });
}

prueba("Una respuesta incompleta o ambigua no se interpreta como éxito", async () => {
    for (const respuesta of [undefined, {}, { exito: "true", estado: "inactivo" }, { exito: true, estado: "activo" }]) {
        const e = escenario({ solicitarBaja: async () => respuesta });
        const fin = terminar(e.boton, "error");
        e.boton.click();
        await fin;
        igual(e.boton.disabled, false);
        igual(e.eventos.some(v => v.tipo === "exito"), false);
    }
});

prueba("Evita doble pulsación durante la confirmación y durante la solicitud", async () => {
    const confirmacion = pendiente();
    const respuesta = pendiente();
    let confirmaciones = 0;
    let solicitudes = 0;
    const e = escenario({
        confirmarBaja: () => { confirmaciones++; return confirmacion.promesa; },
        solicitarBaja: () => { solicitudes++; return respuesta.promesa; }
    });
    const fin = terminar(e.boton, "exito");
    e.boton.click();
    e.boton.dispatchEvent(new MouseEvent("click"));
    igual(confirmaciones, 1);
    confirmacion.resolver(true);
    await pausa();
    e.boton.dispatchEvent(new MouseEvent("click"));
    igual(solicitudes, 1);
    respuesta.resolver({ exito: true, estado: "inactivo" });
    await fin;
});

prueba("Retirar el permiso mientras se confirma impide llamar al backend", async () => {
    const confirmacion = pendiente();
    const e = escenario({ confirmarBaja: () => confirmacion.promesa });
    const fin = terminar(e.boton, "cancelado");
    e.boton.click();
    e.conexion.actualizar({ autorizado: false });
    confirmacion.resolver(true);
    await fin;
    igual(e.boton.hidden, true);
    igual(e.llamadas.baja, 0);
});

prueba("Recuperar el permiso no reutiliza una confirmación anterior", async () => {
    const confirmacion = pendiente();
    const e = escenario({ confirmarBaja: () => confirmacion.promesa });
    const fin = terminar(e.boton, "cancelado");
    e.boton.click();
    e.conexion.actualizar({ autorizado: false });
    e.conexion.actualizar({ autorizado: true });
    confirmacion.resolver(true);
    await fin;
    igual(e.llamadas.baja, 0);
    igual(e.boton.disabled, false);
});

prueba("Revocar el permiso al iniciar la acción cancela incluso antes de abrir T5", async () => {
    const e = escenario();
    e.boton.addEventListener("hu05:baja-estado", evento => {
        if (evento.detail.tipo === "confirmando") e.conexion.actualizar({ autorizado: false });
    });
    const fin = terminar(e.boton, "cancelado");
    e.boton.click();
    await fin;
    igual(e.llamadas.confirmacion, 0);
    igual(e.llamadas.baja, 0);
    igual(e.boton.hidden, true);
});

prueba("Actualizar el miembro a inactivo invalida la confirmación pendiente", async () => {
    const confirmacion = pendiente();
    const e = escenario({ confirmarBaja: () => confirmacion.promesa });
    const fin = terminar(e.boton, "cancelado");
    e.boton.click();
    e.conexion.actualizar({ activo: false });
    confirmacion.resolver(true);
    await fin;
    igual(e.boton.disabled, true);
    igual(e.llamadas.baja, 0);
});

prueba("Retirar el componente durante una solicitud ignora las respuestas tardías", async () => {
    const respuesta = pendiente();
    const e = escenario({ solicitarBaja: () => respuesta.promesa });
    e.boton.click();
    await pausa();
    e.conexion.destruir();
    const numeroEventos = e.eventos.length;
    respuesta.resolver({ exito: true, estado: "inactivo" });
    await pausa();
    igual(e.eventos.length, numeroEventos);
    igual(e.boton.hidden, true);
    igual(e.boton.disabled, true);
});

prueba("Destruir la conexión elimina la acción del botón", async () => {
    const e = escenario();
    e.conexion.destruir();
    e.boton.dispatchEvent(new MouseEvent("click"));
    await pausa();
    igual(e.llamadas.confirmacion, 0);
    igual(e.boton.hidden, true);
});

prueba("Una respuesta antigua no altera el botón reutilizado para otro miembro", async () => {
    const respuesta = pendiente();
    const e = escenario({ solicitarBaja: () => respuesta.promesa });
    e.boton.click();
    await pausa();
    e.conexion.destruir();
    const nuevaConexion = conectarBotonBaja(e.boton, {
        miembroId: 8,
        autorizado: true,
        activo: true,
        confirmarBaja: async () => false,
        solicitarBaja: async () => ({ exito: true, estado: "inactivo" })
    });
    try {
        // La limpieza repetida de una pantalla antigua tampoco retira la conexión nueva.
        e.conexion.destruir();
        respuesta.resolver({ exito: true, estado: "inactivo" });
        await pausa();
        igual(e.boton.hidden, false);
        igual(e.boton.disabled, false);
        igual(e.boton.getAttribute("aria-busy"), "false");
        igual(e.eventos.some(v => v.tipo === "exito"), false);
        const fin = terminar(e.boton, "cancelado");
        e.boton.click();
        igual((await fin).miembroId, 8);
    } finally {
        nuevaConexion.destruir();
    }
});

prueba("Impide conectar dos manejadores de baja al mismo botón", () => {
    const e = escenario();
    let rechazado = false;
    try { conectarBotonBaja(e.boton, { miembroId: 7 }); } catch { rechazado = true; }
    igual(rechazado, true);
});

prueba("Rechaza IDs inválidos antes de instalar el componente", () => {
    for (const miembroId of [undefined, 0, -1, 1.5, "7", 2147483648]) {
        const boton = document.createElement("button");
        let rechazado = false;
        try { conectarBotonBaja(boton, { miembroId }); } catch { rechazado = true; }
        igual(rechazado, true);
    }
});

prueba("Una configuración inválida no aplica parcialmente los cambios", () => {
    const e = escenario({ autorizado: false });
    let rechazado = false;
    try { e.conexion.actualizar({ autorizado: true, solicitarBaja: "endpoint" }); } catch { rechazado = true; }
    igual(rechazado, true);
    igual(e.boton.hidden, true);
});

let aprobadas = 0;
for (const caso of casos) {
    const fila = document.createElement("li");
    try {
        await caso.ejecutar();
        aprobadas++;
        fila.textContent = `✓ ${caso.nombre}`;
        fila.dataset.resultado = "aprobado";
    } catch (error) {
        fila.textContent = `✗ ${caso.nombre}: ${error.message}`;
        fila.dataset.resultado = "fallido";
    } finally {
        for (const e of escenarios.splice(0)) {
            e.conexion.destruir();
            e.boton.remove();
        }
    }
    document.querySelector("#resultados").append(fila);
}
const fallidas = casos.length - aprobadas;
const resumen = document.querySelector("#resumen");
resumen.textContent = `${aprobadas}/${casos.length} pruebas aprobadas · ${fallidas} fallidas`;
resumen.dataset.completado = "true";
resumen.dataset.fallidas = String(fallidas);
document.title = `${fallidas ? "FALLO" : "OK"} · HU05-T4 · ${aprobadas}/${casos.length}`;
