using UnityEngine;

public class CuadriculaEstructural : MonoBehaviour
{
    [Header("Dimensiones de la Plantilla")]
    public int anchoCeldas = 30;
    public int altoCeldas = 30;
    public float tamañoCelda = 10f;

    public Vector3 ObtenerPosicionMundo(int x, int z)
    {
        float posX = (x - (anchoCeldas / 2f)) * tamañoCelda + (tamañoCelda / 2f);
        float posZ = (z - (altoCeldas / 2f)) * tamañoCelda + (tamañoCelda / 2f);
        return new Vector3(posX, 0f, posZ);
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.cyan;
        for (int x = 0; x < anchoCeldas; x++)
        {
            for (int z = 0; z < altoCeldas; z++)
            {
                Gizmos.DrawWireCube(ObtenerPosicionMundo(x, z), new Vector3(tamañoCelda, 0.05f, tamañoCelda));
            }
        }
    }
}
