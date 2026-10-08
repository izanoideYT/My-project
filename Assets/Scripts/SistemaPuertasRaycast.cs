using System.Collections.Generic;
using UnityEngine;

public class SistemaPuertasRaycast : MonoBehaviour
{
    [Header("Referencias")]
    public GeneradorLegos generador;
    public CuadriculaEstructural cuadricula;

    [Header("Raycast")]
    public float distanciaRaycast = 10f;
    public LayerMask capaBloques;

    [Header("Puerta de prueba")]
    public bool crearPuertasDePrueba = true;
    public float alturaPuerta = 1f;
    public float anchoPuerta = 1f;
    public float altoPuerta = 2f;
    public float profundidadPuerta = 0.5f;

    [Header("Debug")]
    public bool mostrarRayos = true;
    public float duracionRayos = 5f;

    private readonly HashSet<string> conexionesDetectadas =
        new HashSet<string>();

    private readonly List<GameObject> puertasCreadas =
        new List<GameObject>();

    // =========================================================
    // ENTRADA PRINCIPAL
    // =========================================================

    public void ComprobarPuertas()
    {
        if (generador == null)
        {
            Debug.LogError(
                "SistemaPuertasRaycast: falta asignar GeneradorLegos."
            );
            return;
        }

        if (cuadricula == null)
        {
            Debug.LogError(
                "SistemaPuertasRaycast: falta asignar CuadriculaEstructural."
            );
            return;
        }

        conexionesDetectadas.Clear();
        LimpiarPuertasDePrueba();

        Debug.Log("===== COMPROBANDO PUERTAS =====");

        for (int x = 0; x < cuadricula.anchoCeldas; x++)
        {
            for (int z = 0; z < cuadricula.altoCeldas; z++)
            {
                int tipo = generador.ObtenerTipoCelda(x, z);

                // 0 = vacío. No necesitamos lanzar Raycast.
                if (tipo == 0)
                    continue;

                ComprobarCelda(x, z, tipo);
            }
        }

        Debug.Log(
            "===== FIN COMPROBACIÓN | Puertas: " +
            conexionesDetectadas.Count +
            " ====="
        );
    }

    // =========================================================
    // CELDA
    // =========================================================

    private void ComprobarCelda(int x, int z, int tipo)
    {
        ComprobarDireccion(x, z, tipo, Vector2Int.right);
        ComprobarDireccion(x, z, tipo, Vector2Int.left);
        ComprobarDireccion(x, z, tipo, Vector2Int.up);
        ComprobarDireccion(x, z, tipo, Vector2Int.down);
    }

    // =========================================================
    // DIRECCIÓN + REGLAS
    // =========================================================

    private void ComprobarDireccion(
        int x,
        int z,
        int tipoOrigen,
        Vector2Int direccion)
    {
        // -----------------------------------------------------
        // PASILLO 1x2
        // -----------------------------------------------------
        // Regla: un bloque de pasillo solo puede tener puerta
        // hacia fuera por una punta. Nunca por sus laterales ni
        // hacia el otro bloque del propio pasillo.
        if (tipoOrigen == 2 &&
            !PuedeSalirDesdePasillo(x, z, direccion))
        {
            DibujarRayoBloqueado(x, z, direccion);
            return;
        }

        Vector3 posicion =
            cuadricula.ObtenerPosicionMundo(x, z);

        Vector3 direccion3D =
            new Vector3(direccion.x, 0f, direccion.y).normalized;

        float mitadCelda =
            cuadricula.tamañoCelda * 0.5f;

        // Salimos desde el borde de la celda, no desde el centro.
        Vector3 origen =
            posicion + direccion3D * mitadCelda;

        RaycastHit hit;

        bool detectado = Physics.Raycast(
            origen,
            direccion3D,
            out hit,
            distanciaRaycast,
            capaBloques,
            QueryTriggerInteraction.Ignore
        );

        if (mostrarRayos)
        {
            Debug.DrawRay(
                origen,
                direccion3D * distanciaRaycast,
                detectado ? Color.green : Color.yellow,
                duracionRayos
            );
        }

        if (!detectado)
            return;

        // Averiguamos qué bloque lógico hemos golpeado.
        Vector2Int celdaImpactada =
            MundoACelda(hit.collider.transform.position);

        if (!DentroDeLaCuadricula(
            celdaImpactada.x,
            celdaImpactada.y))
        {
            return;
        }

        int tipoDestino = generador.ObtenerTipoCelda(
            celdaImpactada.x,
            celdaImpactada.y
        );

        if (tipoDestino == 0)
            return;

        // No queremos puertas entre dos bloques que forman parte
        // de la misma habitación (por ejemplo, dentro de una 2x2).
        if (generador.SonMismaHabitacion(
            x,
            z,
            celdaImpactada.x,
            celdaImpactada.y))
        {
            return;
        }

        string identificador = CrearIdentificadorConexion(
            new Vector3(x, 0f, z),
            new Vector3(
                celdaImpactada.x,
                0f,
                celdaImpactada.y
            )
        );

        if (conexionesDetectadas.Contains(identificador))
            return;

        conexionesDetectadas.Add(identificador);

        CrearPuertaDePrueba(
            hit.point,
            direccion3D,
            tipoOrigen,
            tipoDestino
        );
    }

    // =========================================================
    // REGLA DEL PASILLO
    // =========================================================

    private bool PuedeSalirDesdePasillo(
        int x,
        int z,
        Vector2Int direccion)
    {
        // Detectamos el eje del pasillo mirando sus vecinos.
        // Si hay otro bloque de pasillo a izquierda/derecha,
        // el pasillo es horizontal y sus puntas son izquierda/derecha.
        bool pasilloHorizontal =
            generador.ObtenerTipoCelda(x - 1, z) == 2 ||
            generador.ObtenerTipoCelda(x + 1, z) == 2;

        bool pasilloVertical =
            generador.ObtenerTipoCelda(x, z - 1) == 2 ||
            generador.ObtenerTipoCelda(x, z + 1) == 2;

        // Horizontal: solo izquierda/derecha.
        if (pasilloHorizontal)
        {
            return direccion.x != 0;
        }

        // Vertical: solo arriba/abajo.
        if (pasilloVertical)
        {
            return direccion.y != 0;
        }

        // Si por alguna razón el bloque está aislado, no inventamos
        // una orientación. No permitimos puertas desde él.
        return false;
    }

    private void DibujarRayoBloqueado(
        int x,
        int z,
        Vector2Int direccion)
    {
        if (!mostrarRayos)
            return;

        Vector3 posicion =
            cuadricula.ObtenerPosicionMundo(x, z);

        Vector3 direccion3D =
            new Vector3(direccion.x, 0f, direccion.y).normalized;

        float mitadCelda =
            cuadricula.tamañoCelda * 0.5f;

        Vector3 origen =
            posicion + direccion3D * mitadCelda;

        Debug.DrawRay(
            origen,
            direccion3D * distanciaRaycast,
            Color.red,
            duracionRayos
        );
    }

    // =========================================================
    // PUERTA DE PRUEBA
    // =========================================================

    private void CrearPuertaDePrueba(
        Vector3 punto,
        Vector3 direccion,
        int tipoOrigen,
        int tipoDestino)
    {
        if (!crearPuertasDePrueba)
            return;

        GameObject puerta =
            GameObject.CreatePrimitive(PrimitiveType.Cube);

        puerta.name = "PUERTA_PRUEBA";

        puerta.transform.position = new Vector3(
            punto.x,
            alturaPuerta,
            punto.z
        );

        puerta.transform.localScale = new Vector3(
            anchoPuerta,
            altoPuerta,
            profundidadPuerta
        );

        // Si la conexión es horizontal, giramos la puerta.
        if (Mathf.Abs(direccion.x) > 0.5f)
        {
            puerta.transform.rotation =
                Quaternion.Euler(0f, 90f, 0f);
        }

        puertasCreadas.Add(puerta);

        Debug.Log(
            "PUERTA DETECTADA | " +
            "Origen tipo: " + tipoOrigen +
            " | Destino tipo: " + tipoDestino +
            " | Posición: " + puerta.transform.position
        );
    }

    // =========================================================
    // MUNDO -> CELDA
    // =========================================================

    private Vector2Int MundoACelda(Vector3 posicion)
    {
        // Buscamos la celda cuyo centro está más cerca.
        float mejorDistancia = float.MaxValue;
        Vector2Int mejorCelda = new Vector2Int(-1, -1);

        // La cuadrícula es pequeña (30x30 por defecto), por lo que
        // esta conversión sencilla es suficiente para la prueba.
        for (int x = 0; x < cuadricula.anchoCeldas; x++)
        {
            for (int z = 0; z < cuadricula.altoCeldas; z++)
            {
                Vector3 centro =
                    cuadricula.ObtenerPosicionMundo(x, z);

                float distancia =
                    (new Vector2(centro.x, centro.z) -
                     new Vector2(posicion.x, posicion.z)).sqrMagnitude;

                if (distancia < mejorDistancia)
                {
                    mejorDistancia = distancia;
                    mejorCelda = new Vector2Int(x, z);
                }
            }
        }

        return mejorCelda;
    }

    // =========================================================
    // CONEXIONES
    // =========================================================

    private string CrearIdentificadorConexion(
        Vector3 a,
        Vector3 b)
    {
        int ax = Mathf.RoundToInt(a.x);
        int az = Mathf.RoundToInt(a.z);
        int bx = Mathf.RoundToInt(b.x);
        int bz = Mathf.RoundToInt(b.z);

        if (ax > bx || (ax == bx && az > bz))
        {
            int tx = ax;
            int tz = az;

            ax = bx;
            az = bz;

            bx = tx;
            bz = tz;
        }

        return
            ax + "_" + az + "__" + bx + "_" + bz;
    }

    // =========================================================
    // LIMPIEZA
    // =========================================================

    public void LimpiarPuertasDePrueba()
    {
        foreach (GameObject puerta in puertasCreadas)
        {
            if (puerta != null)
                Destroy(puerta);
        }

        puertasCreadas.Clear();
    }

    private bool DentroDeLaCuadricula(int x, int z)
    {
        return
            x >= 0 &&
            x < cuadricula.anchoCeldas &&
            z >= 0 &&
            z < cuadricula.altoCeldas;
    }
}
    