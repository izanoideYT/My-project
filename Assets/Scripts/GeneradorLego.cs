using System.Collections.Generic;
using UnityEngine;

public class GeneradorLegos : MonoBehaviour
{
    [Header("Semilla")]
    public int semillaManual = 0;
    public int semillaActualUsada;

    [Header("Referencias")]
    public CuadriculaEstructural cuadricula;
    public GameObject celdaBasePrefab;
    public SistemaPuertasRaycast sistemaPuertas;

    [Header("Mapa")]
    public int numeroHabitaciones = 20;

    [Header("Probabilidades")]
    [Range(0f, 1f)]
    public float probabilidadPasillo = 0.35f;

    [Range(0f, 1f)]
    public float probabilidadHabitacion2x2 = 0.30f;

    [Header("Boss")]
    public bool generarBoss = true;

    // Distancia mínima desde la sala inicial para considerar
    // una posición como candidata para la Boss.
    public int distanciaMinimaBoss = 8;

    [Header("Sala Especial ⭐")]
    public bool generarSalaEspecial = true;

    [Header("Colores")]
    public Color colorInicio = Color.green;
    public Color colorNormal = Color.white;
    public Color colorPasillo = Color.cyan;
    public Color color2x2 = Color.yellow;
    public Color colorBoss = Color.red;
    public Color colorSalaEspecial = new Color(0.75f, 0.2f, 1f);

    // 0 = vacío
    // 1 = habitación 1x1
    // 2 = pasillo 1x2
    // 3 = habitación 2x2
    // 4 = Boss 2x2
    // 5 = Sala Especial 1x1 (terminal, una sola entrada)

    private int[,] mapaLogico;

    private List<GameObject> instancias =
        new List<GameObject>();

    private List<Habitacion> habitaciones =
        new List<Habitacion>();

    private class Habitacion
    {
        public int x;
        public int z;
        public int ancho;
        public int alto;

        public bool arriba;
        public bool abajo;
        public bool izquierda;
        public bool derecha;

        // Las salas terminales no pueden generar nuevas conexiones.
        public bool terminal;

        public Habitacion(
            int x,
            int z,
            int ancho,
            int alto,
            bool terminal = false)
        {
            this.x = x;
            this.z = z;
            this.ancho = ancho;
            this.alto = alto;
            this.terminal = terminal;
        }

        public bool TieneConexion(Vector2Int direccion)
        {
            if (direccion.x == 1)
                return derecha;

            if (direccion.x == -1)
                return izquierda;

            if (direccion.y == 1)
                return arriba;

            return abajo;
        }

        public void ActivarConexion(Vector2Int direccion)
        {
            if (direccion.x == 1)
                derecha = true;
            else if (direccion.x == -1)
                izquierda = true;
            else if (direccion.y == 1)
                arriba = true;
            else
                abajo = true;
        }
    }

    private class CandidatoConexion
    {
        public Habitacion origen;
        public Vector2Int direccion;

        public CandidatoConexion(
            Habitacion origen,
            Vector2Int direccion)
        {
            this.origen = origen;
            this.direccion = direccion;
        }
    }

    private void Start()
    {
        GenerarMapa();
    }

    private void GenerarMapa()
    {
        PrepararSemilla();
        LimpiarObjetos();

        mapaLogico = new int[
            cuadricula.anchoCeldas,
            cuadricula.altoCeldas
        ];

        int centroX =
            cuadricula.anchoCeldas / 2;

        int centroZ =
            cuadricula.altoCeldas / 2;

        Habitacion inicial =
            new Habitacion(
                centroX,
                centroZ,
                1,
                1
            );

        habitaciones.Add(inicial);

        ColocarHabitacion(
            inicial,
            colorInicio
        );

        int intentos = 0;
        int maxIntentos =
            Mathf.Max(
                10000,
                numeroHabitaciones * 1000
            );

        // En cada vuelta probamos TODAS las conexiones
        // posibles en orden aleatorio. Así no dependemos
        // de acertar por casualidad una dirección válida.
        while (
            habitaciones.Count < numeroHabitaciones &&
            intentos < maxIntentos)
        {
            intentos++;

            if (!IntentarCrearUnaHabitacion())
            {
                // No quedan conexiones válidas.
                break;
            }
        }

        // La Boss se crea al final y se busca primero una
        // posición lejana. Si la distancia mínima impide
        // encontrarla, CrearBoss hace una segunda búsqueda
        // sin esa restricción para que no desaparezca.
        if (generarBoss)
        {
            CrearBoss(
                centroX,
                centroZ
            );
        }

        // La sala especial se crea al final para que sea terminal:
        // exactamente una conexión y nunca genera más habitaciones.
        if (generarSalaEspecial)
        {
            CrearSalaEspecial();
        }

        Debug.Log(
            "MAPA GENERADO | " +
            "Habitaciones: " +
            habitaciones.Count +
            " | Intentos: " +
            intentos +
            " | Seed: " +
            semillaActualUsada
        );

        // Una vez terminado el mapa, ejecutamos el sistema de puertas.
        // Así nos aseguramos de que los Raycast se hagan sobre un mapa ya creado.
        if (sistemaPuertas != null)
        {
            sistemaPuertas.ComprobarPuertas();
        }
        else
        {
            Debug.LogWarning(
                "GeneradorLegos: no hay SistemaPuertasRaycast asignado. " +
                "El mapa se generará igualmente."
            );
        }
    }

    private bool IntentarCrearUnaHabitacion()
    {
        List<CandidatoConexion> candidatos =
            new List<CandidatoConexion>();

        Vector2Int[] direcciones =
        {
            new Vector2Int(1, 0),
            new Vector2Int(-1, 0),
            new Vector2Int(0, 1),
            new Vector2Int(0, -1)
        };

        foreach (Habitacion habitacion in habitaciones)
        {
            if (habitacion.terminal)
                continue;

            foreach (Vector2Int direccion in direcciones)
            {
                if (
                    !habitacion.TieneConexion(
                        direccion))
                {
                    candidatos.Add(
                        new CandidatoConexion(
                            habitacion,
                            direccion
                        )
                    );
                }
            }
        }

        // Mezclar candidatos para mantener la generación
        // procedural y dependiente de la seed.
        for (
            int i = candidatos.Count - 1;
            i > 0;
            i--)
        {
            int j = Random.Range(0, i + 1);

            CandidatoConexion temporal =
                candidatos[i];

            candidatos[i] =
                candidatos[j];

            candidatos[j] =
                temporal;
        }

        // Probamos cada conexión posible. Si una falla,
        // probamos otra habitación/dirección.
        foreach (
            CandidatoConexion candidato
            in candidatos)
        {
            if (
                IntentarCrearHabitacion(
                    candidato.origen,
                    candidato.direccion))
            {
                return true;
            }
        }

        return false;
    }

    private bool IntentarCrearHabitacion(
        Habitacion origen,
        Vector2Int direccion)
    {
        bool usarPasillo =
            Random.value < probabilidadPasillo;

        int columna =
            Random.Range(0, origen.ancho);

        int fila =
            Random.Range(0, origen.alto);

        int salidaX;
        int salidaZ;

        if (direccion.x == 1)
        {
            salidaX =
                origen.x +
                origen.ancho -
                1;

            salidaZ =
                origen.z +
                fila;
        }
        else if (direccion.x == -1)
        {
            salidaX =
                origen.x;

            salidaZ =
                origen.z +
                fila;
        }
        else if (direccion.y == 1)
        {
            salidaX =
                origen.x +
                columna;

            salidaZ =
                origen.z +
                origen.alto -
                1;
        }
        else
        {
            salidaX =
                origen.x +
                columna;

            salidaZ =
                origen.z;
        }

        int p1X = salidaX;
        int p1Z = salidaZ;
        int p2X = salidaX;
        int p2Z = salidaZ;

        int finalX = salidaX;
        int finalZ = salidaZ;

        // -------------------------------------------------
        // PRIMERO VALIDAMOS EL PASILLO.
        // TODAVÍA NO COLOCAMOS NINGÚN BLOQUE.
        // -------------------------------------------------

        if (usarPasillo)
        {
            p1X =
                salidaX +
                direccion.x;

            p1Z =
                salidaZ +
                direccion.y;

            p2X =
                salidaX +
                direccion.x * 2;

            p2Z =
                salidaZ +
                direccion.y * 2;

            if (
                !DentroDeLaCuadricula(
                    p1X,
                    p1Z) ||
                !DentroDeLaCuadricula(
                    p2X,
                    p2Z))
            {
                return false;
            }

            if (
                mapaLogico[p1X, p1Z] != 0 ||
                mapaLogico[p2X, p2Z] != 0)
            {
                return false;
            }

            if (
                !PasilloPermitido(
                    p1X,
                    p1Z,
                    p2X,
                    p2Z,
                    direccion))
            {
                return false;
            }

            finalX = p2X;
            finalZ = p2Z;
        }

        // -------------------------------------------------
        // AHORA CALCULAMOS LA HABITACIÓN.
        // -------------------------------------------------

        bool es2x2 =
            Random.value <
            probabilidadHabitacion2x2;

        int ancho =
            es2x2 ? 2 : 1;

        int alto =
            es2x2 ? 2 : 1;

        int nuevaX;
        int nuevaZ;

        if (direccion.x == 1)
        {
            nuevaX =
                finalX + 1;

            nuevaZ =
                finalZ -
                (es2x2 ? fila : 0);
        }
        else if (direccion.x == -1)
        {
            nuevaX =
                finalX -
                ancho;

            nuevaZ =
                finalZ -
                (es2x2 ? fila : 0);
        }
        else if (direccion.y == 1)
        {
            nuevaX =
                finalX -
                (es2x2 ? columna : 0);

            nuevaZ =
                finalZ + 1;
        }
        else
        {
            nuevaX =
                finalX -
                (es2x2 ? columna : 0);

            nuevaZ =
                finalZ -
                alto;
        }

        // MUY IMPORTANTE:
        // comprobamos la habitación antes de colocar
        // los bloques del pasillo.
        if (
            !EspacioDisponible(
                nuevaX,
                nuevaZ,
                ancho,
                alto))
        {
            return false;
        }

        // -------------------------------------------------
        // A PARTIR DE AQUÍ LA CONEXIÓN ES VÁLIDA.
        // PODEMOS COLOCAR TODO.
        // -------------------------------------------------

        if (usarPasillo)
        {
            mapaLogico[p1X, p1Z] = 2;
            mapaLogico[p2X, p2Z] = 2;

            CrearBloque(
                p1X,
                p1Z,
                colorPasillo
            );

            CrearBloque(
                p2X,
                p2Z,
                colorPasillo
            );
        }

        Habitacion nueva =
            new Habitacion(
                nuevaX,
                nuevaZ,
                ancho,
                alto
            );

        habitaciones.Add(nueva);

        Color color =
            es2x2
            ? color2x2
            : colorNormal;

        ColocarHabitacion(
            nueva,
            color
        );

        origen.ActivarConexion(
            direccion
        );

        nueva.ActivarConexion(
            new Vector2Int(
                -direccion.x,
                -direccion.y
            )
        );

        return true;
    }


    private class CandidatoSalaEspecial
    {
        public Habitacion origen;
        public Vector2Int direccion;
        public int especialX;
        public int especialZ;

        public CandidatoSalaEspecial(
            Habitacion origen,
            Vector2Int direccion,
            int especialX,
            int especialZ)
        {
            this.origen = origen;
            this.direccion = direccion;
            this.especialX = especialX;
            this.especialZ = especialZ;
        }
    }

    // =========================================================
    // CREAR SALA ESPECIAL ⭐
    // =========================================================
    //
    // Es una sala 1x1 terminal, parecida a una sala especial
    // de The Binding of Isaac. Puede aparecer en cualquier
    // posición libre que tenga una conexión válida de pasillo.
    // Una vez creada, no puede generar ninguna otra sala.
    //
    private void CrearSalaEspecial()
    {
        // IMPORTANTE:
        // La sala especial NO se busca en la posición más lejana del mapa.
        // Eso hacía que pareciera una "sala final" pegada al extremo de
        // una rama, que no es el comportamiento que buscamos.
        //
        // En su lugar, la tratamos como una sala especial de Isaac:
        // se crea desde UNA conexión normal ya existente, en una posición
        // válida del mapa, y desde ella NO puede salir ninguna otra sala.

        List<CandidatoSalaEspecial> candidatos =
            new List<CandidatoSalaEspecial>();

        Vector2Int[] direcciones =
        {
            new Vector2Int(1, 0),
            new Vector2Int(-1, 0),
            new Vector2Int(0, 1),
            new Vector2Int(0, -1)
        };

        foreach (Habitacion habitacion in habitaciones)
        {
            if (habitacion.terminal)
                continue;

            foreach (Vector2Int direccion in direcciones)
            {
                if (habitacion.TieneConexion(direccion))
                    continue;

                int cantidadSalidas =
                    direccion.x != 0
                    ? habitacion.alto
                    : habitacion.ancho;

                for (int i = 0; i < cantidadSalidas; i++)
                {
                    int salidaX;
                    int salidaZ;

                    if (direccion.x == 1)
                    {
                        salidaX = habitacion.x + habitacion.ancho - 1;
                        salidaZ = habitacion.z + i;
                    }
                    else if (direccion.x == -1)
                    {
                        salidaX = habitacion.x;
                        salidaZ = habitacion.z + i;
                    }
                    else if (direccion.y == 1)
                    {
                        salidaX = habitacion.x + i;
                        salidaZ = habitacion.z + habitacion.alto - 1;
                    }
                    else
                    {
                        salidaX = habitacion.x + i;
                        salidaZ = habitacion.z;
                    }

                    int p1X = salidaX + direccion.x;
                    int p1Z = salidaZ + direccion.y;
                    int p2X = salidaX + direccion.x * 2;
                    int p2Z = salidaZ + direccion.y * 2;

                    int especialX = p2X + direccion.x;
                    int especialZ = p2Z + direccion.y;

                    if (!EspacioDisponible(especialX, especialZ, 1, 1))
                        continue;

                    if (!DentroDeLaCuadricula(p1X, p1Z) ||
                        !DentroDeLaCuadricula(p2X, p2Z) ||
                        !DentroDeLaCuadricula(especialX, especialZ))
                    {
                        continue;
                    }

                    if (mapaLogico[p1X, p1Z] != 0 ||
                        mapaLogico[p2X, p2Z] != 0)
                    {
                        continue;
                    }

                    if (!PasilloPermitido(
                        p1X,
                        p1Z,
                        p2X,
                        p2Z,
                        direccion))
                    {
                        continue;
                    }

                    candidatos.Add(
                        new CandidatoSalaEspecial(
                            habitacion,
                            direccion,
                            especialX,
                            especialZ));
                }
            }
        }

        if (candidatos.Count == 0)
        {
            Debug.LogWarning(
                "NO SE PUDO CREAR LA SALA ESPECIAL. " +
                "No existe ninguna conexión 1x2 válida.");
            return;
        }

        // Elegimos una conexión válida al azar.
        // Así la sala puede aparecer en cualquier rama del mapa,
        // en lugar de convertirse siempre en la habitación más lejana.
        CandidatoSalaEspecial candidato =
            candidatos[Random.Range(0, candidatos.Count)];

        if (!CrearConexionSalaEspecial(
            candidato.origen,
            candidato.direccion,
            candidato.especialX,
            candidato.especialZ))
        {
            Debug.LogWarning(
                "La posición encontrada para la Sala Especial " +
                "dejó de ser válida al crear la conexión.");
            return;
        }

        // Es TERMINAL desde el momento de su creación.
        Habitacion especial = new Habitacion(
            candidato.especialX,
            candidato.especialZ,
            1,
            1,
            true);

        habitaciones.Add(especial);

        mapaLogico[
            candidato.especialX,
            candidato.especialZ] = 5;

        CrearBloque(
            candidato.especialX,
            candidato.especialZ,
            colorSalaEspecial);

        candidato.origen.ActivarConexion(
            candidato.direccion);

        especial.ActivarConexion(
            new Vector2Int(
                -candidato.direccion.x,
                -candidato.direccion.y));

        Debug.Log(
            "SALA ESPECIAL CREADA ⭐ | Posición: " +
            candidato.especialX + ", " +
            candidato.especialZ +
            " | Entrada: " + candidato.direccion);
    }

    private bool ExisteConexionSalaEspecial(
        Habitacion origen,
        Vector2Int direccion,
        int especialX,
        int especialZ)
    {
        int cantidadSalidas =
            direccion.x != 0
            ? origen.alto
            : origen.ancho;

        for (int i = 0; i < cantidadSalidas; i++)
        {
            int salidaX;
            int salidaZ;

            if (direccion.x == 1)
            {
                salidaX = origen.x + origen.ancho - 1;
                salidaZ = origen.z + i;
            }
            else if (direccion.x == -1)
            {
                salidaX = origen.x;
                salidaZ = origen.z + i;
            }
            else if (direccion.y == 1)
            {
                salidaX = origen.x + i;
                salidaZ = origen.z + origen.alto - 1;
            }
            else
            {
                salidaX = origen.x + i;
                salidaZ = origen.z;
            }

            int p1X = salidaX + direccion.x;
            int p1Z = salidaZ + direccion.y;
            int p2X = salidaX + direccion.x * 2;
            int p2Z = salidaZ + direccion.y * 2;

            if (!DentroDeLaCuadricula(p1X, p1Z) ||
                !DentroDeLaCuadricula(p2X, p2Z))
            {
                continue;
            }

            // El segundo bloque del pasillo queda justo antes
            // de la sala especial.
            if (p2X + direccion.x != especialX ||
                p2Z + direccion.y != especialZ)
                continue;

            if (mapaLogico[p1X, p1Z] != 0 ||
                mapaLogico[p2X, p2Z] != 0)
            {
                // p2 es precisamente la sala especial, así que
                // aquí todavía debe estar libre.
                continue;
            }

            if (!PasilloPermitido(
                p1X,
                p1Z,
                p2X,
                p2Z,
                direccion))
            {
                continue;
            }

            return true;
        }

        return false;
    }

    private bool CrearConexionSalaEspecial(
        Habitacion origen,
        Vector2Int direccion,
        int especialX,
        int especialZ)
    {
        int cantidadSalidas =
            direccion.x != 0
            ? origen.alto
            : origen.ancho;

        for (int i = 0; i < cantidadSalidas; i++)
        {
            int salidaX;
            int salidaZ;

            if (direccion.x == 1)
            {
                salidaX = origen.x + origen.ancho - 1;
                salidaZ = origen.z + i;
            }
            else if (direccion.x == -1)
            {
                salidaX = origen.x;
                salidaZ = origen.z + i;
            }
            else if (direccion.y == 1)
            {
                salidaX = origen.x + i;
                salidaZ = origen.z + origen.alto - 1;
            }
            else
            {
                salidaX = origen.x + i;
                salidaZ = origen.z;
            }

            int p1X = salidaX + direccion.x;
            int p1Z = salidaZ + direccion.y;
            int p2X = salidaX + direccion.x * 2;
            int p2Z = salidaZ + direccion.y * 2;

            if (!DentroDeLaCuadricula(p1X, p1Z) ||
                !DentroDeLaCuadricula(p2X, p2Z))
            {
                continue;
            }

            // El segundo bloque del pasillo queda justo antes
            // de la sala especial.
            if (p2X + direccion.x != especialX ||
                p2Z + direccion.y != especialZ)
                continue;

            if (mapaLogico[p1X, p1Z] != 0 ||
                mapaLogico[p2X, p2Z] != 0)
            {
                continue;
            }

            if (!PasilloPermitido(
                p1X,
                p1Z,
                p2X,
                p2Z,
                direccion))
            {
                continue;
            }

            mapaLogico[p1X, p1Z] = 2;
            mapaLogico[p2X, p2Z] = 2;

            CrearBloque(p1X, p1Z, colorPasillo);
            CrearBloque(p2X, p2Z, colorPasillo);

            return true;
        }

        return false;
    }

    // =========================================================
    // CREAR BOSS
    // =========================================================
    //
    // La Boss se busca en dos fases:
    //
    // 1) Primero intenta encontrar la posición más lejana
    //    respetando distanciaMinimaBoss.
    //
    // 2) Si no existe ninguna, vuelve a buscar sin la
    //    distancia mínima. Así la Boss no desaparece.
    //
    // En ambos casos la posición SOLO es válida si existe
    // una conexión REAL de 1x2 desde una habitación existente.
    //
    private void CrearBoss(
        int centroX,
        int centroZ)
    {
        Habitacion mejorOrigen = null;
        Vector2Int mejorDireccion = Vector2Int.zero;

        int mejorBossX = -1;
        int mejorBossZ = -1;

        float mejorDistancia = -1f;

        // PRIMERA BÚSQUEDA:
        // respetando distanciaMinimaBoss.
        BuscarPosicionBoss(
            centroX,
            centroZ,
            distanciaMinimaBoss,
            ref mejorOrigen,
            ref mejorDireccion,
            ref mejorBossX,
            ref mejorBossZ,
            ref mejorDistancia
        );

        // SEGUNDA BÚSQUEDA:
        // si no encontramos ninguna, quitamos la distancia mínima.
        if (mejorOrigen == null)
        {
            mejorDistancia = -1f;

            BuscarPosicionBoss(
                centroX,
                centroZ,
                0,
                ref mejorOrigen,
                ref mejorDireccion,
                ref mejorBossX,
                ref mejorBossZ,
                ref mejorDistancia
            );
        }

        // Si ni siquiera así hay espacio, no hay una conexión
        // 1x2 posible con el mapa actual.
        if (mejorOrigen == null)
        {
            Debug.LogWarning(
                "NO SE PUDO CREAR LA BOSS. " +
                "No existe ningún espacio 2x2 libre " +
                "con una conexión 1x2 válida."
            );

            return;
        }

        // Crear primero el pasillo.
        if (
            !CrearConexionBoss(
                mejorOrigen,
                mejorDireccion,
                mejorBossX,
                mejorBossZ))
        {
            Debug.LogWarning(
                "La posición encontrada para la Boss " +
                "dejó de ser válida al crear la conexión."
            );

            return;
        }

        // Crear la habitación Boss 2x2.
        Habitacion boss =
            new Habitacion(
                mejorBossX,
                mejorBossZ,
                2,
                2,
                true
            );

        habitaciones.Add(boss);

        for (int x = 0; x < 2; x++)
        {
            for (int z = 0; z < 2; z++)
            {
                int bx =
                    mejorBossX + x;

                int bz =
                    mejorBossZ + z;

                mapaLogico[bx, bz] = 4;

                CrearBloque(
                    bx,
                    bz,
                    colorBoss
                );
            }
        }

        // Registrar las conexiones.
        mejorOrigen.ActivarConexion(
            mejorDireccion
        );

        boss.ActivarConexion(
            new Vector2Int(
                -mejorDireccion.x,
                -mejorDireccion.y
            )
        );

        Debug.Log(
            "BOSS CREADO SIEMPRE QUE EXISTE " +
            "UNA CONEXIÓN VÁLIDA | " +
            "Posición: " +
            mejorBossX +
            ", " +
            mejorBossZ +
            " | Distancia: " +
            mejorDistancia
        );
    }

    // =========================================================
    // BUSCAR POSICIÓN DE BOSS
    // =========================================================

    private void BuscarPosicionBoss(
        int centroX,
        int centroZ,
        float distanciaMinima,
        ref Habitacion mejorOrigen,
        ref Vector2Int mejorDireccion,
        ref int mejorBossX,
        ref int mejorBossZ,
        ref float mejorDistancia)
    {
        Vector2Int[] direcciones =
        {
            new Vector2Int(1, 0),
            new Vector2Int(-1, 0),
            new Vector2Int(0, 1),
            new Vector2Int(0, -1)
        };

        for (
            int bossX = 0;
            bossX < cuadricula.anchoCeldas - 1;
            bossX++)
        {
            for (
                int bossZ = 0;
                bossZ < cuadricula.altoCeldas - 1;
                bossZ++)
            {
                // La Boss siempre ocupa 2x2.
                if (
                    !EspacioDisponible(
                        bossX,
                        bossZ,
                        2,
                        2))
                {
                    continue;
                }

                float distancia =
                    Vector2.Distance(
                        new Vector2(
                            centroX,
                            centroZ),
                        new Vector2(
                            bossX + 0.5f,
                            bossZ + 0.5f)
                    );

                if (
                    distancia < distanciaMinima)
                {
                    continue;
                }

                foreach (
                    Habitacion habitacion
                    in habitaciones)
                {
                    foreach (
                        Vector2Int direccion
                        in direcciones)
                    {
                        // No reutilizar una puerta ya usada.
                        if (
                            habitacion.TieneConexion(
                                direccion))
                        {
                            continue;
                        }

                        if (
                            !ExisteConexionBoss(
                                habitacion,
                                direccion,
                                bossX,
                                bossZ))
                        {
                            continue;
                        }

                        if (
                            distancia >
                            mejorDistancia)
                        {
                            mejorDistancia =
                                distancia;

                            mejorBossX =
                                bossX;

                            mejorBossZ =
                                bossZ;

                            mejorOrigen =
                                habitacion;

                            mejorDireccion =
                                direccion;
                        }
                    }
                }
            }
        }
    }

    private bool ExisteConexionBoss(
        Habitacion origen,
        Vector2Int direccion,
        int bossX,
        int bossZ)
    {
        int cantidadSalidas =
            direccion.x != 0
            ? origen.alto
            : origen.ancho;

        for (
            int i = 0;
            i < cantidadSalidas;
            i++)
        {
            int salidaX;
            int salidaZ;

            if (direccion.x == 1)
            {
                salidaX =
                    origen.x +
                    origen.ancho -
                    1;

                salidaZ =
                    origen.z + i;
            }
            else if (direccion.x == -1)
            {
                salidaX =
                    origen.x;

                salidaZ =
                    origen.z + i;
            }
            else if (direccion.y == 1)
            {
                salidaX =
                    origen.x + i;

                salidaZ =
                    origen.z +
                    origen.alto -
                    1;
            }
            else
            {
                salidaX =
                    origen.x + i;

                salidaZ =
                    origen.z;
            }

            int p1X =
                salidaX +
                direccion.x;

            int p1Z =
                salidaZ +
                direccion.y;

            int p2X =
                salidaX +
                direccion.x * 2;

            int p2Z =
                salidaZ +
                direccion.y * 2;

            if (
                !DentroDeLaCuadricula(
                    p1X,
                    p1Z) ||
                !DentroDeLaCuadricula(
                    p2X,
                    p2Z))
            {
                continue;
            }

            if (
                mapaLogico[p1X, p1Z] != 0 ||
                mapaLogico[p2X, p2Z] != 0)
            {
                continue;
            }

            // El segundo bloque del pasillo debe tocar
            // directamente una de las celdas de la Boss.
            bool tocaBoss = false;

            if (direccion.x == 1)
            {
                tocaBoss =
                    p2X + 1 == bossX &&
                    p2Z >= bossZ &&
                    p2Z <= bossZ + 1;
            }
            else if (direccion.x == -1)
            {
                tocaBoss =
                    p2X - 1 == bossX + 1 &&
                    p2Z >= bossZ &&
                    p2Z <= bossZ + 1;
            }
            else if (direccion.y == 1)
            {
                tocaBoss =
                    p2Z + 1 == bossZ &&
                    p2X >= bossX &&
                    p2X <= bossX + 1;
            }
            else
            {
                tocaBoss =
                    p2Z - 1 == bossZ + 1 &&
                    p2X >= bossX &&
                    p2X <= bossX + 1;
            }

            if (!tocaBoss)
            {
                continue;
            }

            if (
                !PasilloPermitido(
                    p1X,
                    p1Z,
                    p2X,
                    p2Z,
                    direccion))
            {
                continue;
            }

            return true;
        }

        return false;
    }

    // =========================================================
    // CREAR CONEXIÓN REAL CON LA BOSS
    // =========================================================

    private bool CrearConexionBoss(
        Habitacion origen,
        Vector2Int direccion,
        int bossX,
        int bossZ)
    {
        int cantidadSalidas =
            direccion.x != 0
            ? origen.alto
            : origen.ancho;

        for (
            int i = 0;
            i < cantidadSalidas;
            i++)
        {
            int salidaX;
            int salidaZ;

            if (direccion.x == 1)
            {
                salidaX =
                    origen.x +
                    origen.ancho -
                    1;

                salidaZ =
                    origen.z + i;
            }
            else if (direccion.x == -1)
            {
                salidaX =
                    origen.x;

                salidaZ =
                    origen.z + i;
            }
            else if (direccion.y == 1)
            {
                salidaX =
                    origen.x + i;

                salidaZ =
                    origen.z +
                    origen.alto -
                    1;
            }
            else
            {
                salidaX =
                    origen.x + i;

                salidaZ =
                    origen.z;
            }

            int p1X =
                salidaX +
                direccion.x;

            int p1Z =
                salidaZ +
                direccion.y;

            int p2X =
                salidaX +
                direccion.x * 2;

            int p2Z =
                salidaZ +
                direccion.y * 2;

            if (
                !DentroDeLaCuadricula(
                    p1X,
                    p1Z) ||
                !DentroDeLaCuadricula(
                    p2X,
                    p2Z))
            {
                continue;
            }

            if (
                mapaLogico[p1X, p1Z] != 0 ||
                mapaLogico[p2X, p2Z] != 0)
            {
                continue;
            }

            if (
                !PasilloPermitido(
                    p1X,
                    p1Z,
                    p2X,
                    p2Z,
                    direccion))
            {
                continue;
            }

            bool tocaBoss = false;

            if (direccion.x == 1)
            {
                tocaBoss =
                    p2X + 1 == bossX &&
                    p2Z >= bossZ &&
                    p2Z <= bossZ + 1;
            }
            else if (direccion.x == -1)
            {
                tocaBoss =
                    p2X - 1 == bossX + 1 &&
                    p2Z >= bossZ &&
                    p2Z <= bossZ + 1;
            }
            else if (direccion.y == 1)
            {
                tocaBoss =
                    p2Z + 1 == bossZ &&
                    p2X >= bossX &&
                    p2X <= bossX + 1;
            }
            else
            {
                tocaBoss =
                    p2Z - 1 == bossZ + 1 &&
                    p2X >= bossX &&
                    p2X <= bossX + 1;
            }

            if (!tocaBoss)
            {
                continue;
            }

            mapaLogico[p1X, p1Z] = 2;
            mapaLogico[p2X, p2Z] = 2;

            CrearBloque(
                p1X,
                p1Z,
                colorPasillo
            );

            CrearBloque(
                p2X,
                p2Z,
                colorPasillo
            );

            return true;
        }

        return false;
    }

    // =========================================================
    // REGLAS DE PASILLO
    // =========================================================

    private bool PasilloPermitido(
        int x1,
        int z1,
        int x2,
        int z2,
        Vector2Int direccion)
    {
        int a1X;
        int a1Z;
        int b1X;
        int b1Z;

        int a2X;
        int a2Z;
        int b2X;
        int b2Z;

        if (direccion.x == 0)
        {
            // Pasillo vertical.
            // Comprobamos sus dos lados.
            a1X = x1 - 1;
            a1Z = z1;

            b1X = x1 + 1;
            b1Z = z1;

            a2X = x2 - 1;
            a2Z = z2;

            b2X = x2 + 1;
            b2Z = z2;
        }
        else
        {
            // Pasillo horizontal.
            a1X = x1;
            a1Z = z1 - 1;

            b1X = x1;
            b1Z = z1 + 1;

            a2X = x2;
            a2Z = z2 - 1;

            b2X = x2;
            b2Z = z2 + 1;
        }

        bool a1 =
            CeldaOcupada(a1X, a1Z);

        bool b1 =
            CeldaOcupada(b1X, b1Z);

        bool a2 =
            CeldaOcupada(a2X, a2Z);

        bool b2 =
            CeldaOcupada(b2X, b2Z);

        // No permitimos que una pieza del pasillo
        // tenga estructuras ocupando simultáneamente
        // sus dos laterales.
        if (a1 && b1)
            return false;

        if (a2 && b2)
            return false;

        return true;
    }

    // =========================================================
    // HABITACIONES
    // =========================================================

    private void ColocarHabitacion(
        Habitacion habitacion,
        Color color)
    {
        for (
            int x = 0;
            x < habitacion.ancho;
            x++)
        {
            for (
                int z = 0;
                z < habitacion.alto;
                z++)
            {
                int mapaX =
                    habitacion.x + x;

                int mapaZ =
                    habitacion.z + z;

                mapaLogico[
                    mapaX,
                    mapaZ
                ] =
                    habitacion.ancho == 2
                    ? 3
                    : 1;

                CrearBloque(
                    mapaX,
                    mapaZ,
                    color
                );
            }
        }
    }

    private bool EspacioDisponible(
        int x,
        int z,
        int ancho,
        int alto)
    {
        if (
            x < 0 ||
            z < 0 ||
            x + ancho >
                cuadricula.anchoCeldas ||
            z + alto >
                cuadricula.altoCeldas)
        {
            return false;
        }

        for (
            int ix = 0;
            ix < ancho;
            ix++)
        {
            for (
                int iz = 0;
                iz < alto;
                iz++)
            {
                if (
                    mapaLogico[
                        x + ix,
                        z + iz
                    ] != 0)
                {
                    return false;
                }
            }
        }

        return true;
    }

    // =========================================================
    // DIRECCIONES
    // =========================================================

    private Vector2Int ObtenerDireccionAleatoria()
    {
        switch (
            Random.Range(0, 4))
        {
            case 0:
                return new Vector2Int(1, 0);

            case 1:
                return new Vector2Int(-1, 0);

            case 2:
                return new Vector2Int(0, 1);

            default:
                return new Vector2Int(0, -1);
        }
    }

    private bool CeldaOcupada(
        int x,
        int z)
    {
        if (
            !DentroDeLaCuadricula(
                x,
                z))
        {
            return false;
        }

        return mapaLogico[x, z] != 0;
    }

    private bool DentroDeLaCuadricula(
        int x,
        int z)
    {
        return
            x >= 0 &&
            x < cuadricula.anchoCeldas &&
            z >= 0 &&
            z < cuadricula.altoCeldas;
    }

    // =========================================================
    // CREAR BLOQUES
    // =========================================================

    private void CrearBloque(
        int x,
        int z,
        Color color)
    {
        Vector3 posicion =
            cuadricula.ObtenerPosicionMundo(
                x,
                z
            );

        GameObject bloque =
            Instantiate(
                celdaBasePrefab,
                posicion,
                Quaternion.identity,
                transform
            );

        Renderer render =
            bloque.GetComponentInChildren<Renderer>();

        if (render != null)
        {
            render.material.color =
                color;
        }

        instancias.Add(
            bloque
        );
    }

    // =========================================================
    // SEMILLA
    // =========================================================

    private void PrepararSemilla()
    {
        semillaActualUsada =
            semillaManual == 0
            ? Random.Range(
                1,
                int.MaxValue)
            : semillaManual;

        Random.InitState(
            semillaActualUsada
        );

        Debug.Log(
            "Seed utilizada: " +
            semillaActualUsada
        );
    }

    // =========================================================
    // ACCESO PARA EL SISTEMA DE PUERTAS
    // =========================================================

    public bool SonMismaHabitacion(int x1, int z1, int x2, int z2)
    {
        foreach (Habitacion habitacion in habitaciones)
        {
            bool primeroDentro =
                x1 >= habitacion.x &&
                x1 < habitacion.x + habitacion.ancho &&
                z1 >= habitacion.z &&
                z1 < habitacion.z + habitacion.alto;

            if (!primeroDentro)
                continue;

            bool segundoDentro =
                x2 >= habitacion.x &&
                x2 < habitacion.x + habitacion.ancho &&
                z2 >= habitacion.z &&
                z2 < habitacion.z + habitacion.alto;

            if (segundoDentro)
                return true;
        }

        return false;
    }

    public int ObtenerTipoCelda(int x, int z)
    {
        if (!DentroDeLaCuadricula(x, z))
            return 0;

        return mapaLogico[x, z];
    }

    // =========================================================
    // LIMPIAR
    // =========================================================

    private void LimpiarObjetos()
    {
        foreach (
            GameObject objeto
            in instancias)
        {
            if (objeto != null)
            {
                Destroy(objeto);
            }
        }

        instancias.Clear();
        habitaciones.Clear();
    }
}
