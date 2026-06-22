using UnityEngine;

public class CursorController : MonoBehaviour
{
    bool lastAltState = false;
    CameraControler cameraControler;
    private KontrolPemain kontrolPemain;

    void Awake()
    {
        kontrolPemain = new KontrolPemain();
        cameraControler = GetComponent<CameraControler>();
    }

    void OnEnable()
    {
        kontrolPemain?.Pemain.Enable();
    }

    void OnDisable()
    {
        kontrolPemain?.Pemain.Disable();
    }

    void OnDestroy()
    {
        kontrolPemain?.Dispose();
    }

    void Start()
    {
        SembunyikanCursor();
    }

    void Update()
    {
        bool altPressed = kontrolPemain != null && kontrolPemain.Pemain.ShowCursor.IsPressed();

        if (altPressed)
        {
            if (Cursor.lockState != CursorLockMode.None || !Cursor.visible)
            {
                TampilkanCursor();
            }
        }
        else
        {
            if (Cursor.lockState != CursorLockMode.Locked || Cursor.visible)
            {
                SembunyikanCursor();
            }
        }

        if (altPressed != lastAltState)
        {
            Debug.LogWarning(altPressed
                ? "Pointer Tampil Anda Dapat berinteraksi dengan UI"
                : "Pointer Tersembunyi Anda Dapat mengendalikan karakter");
            lastAltState = altPressed;
        }
    }

    void SembunyikanCursor()
    {
        Cursor.visible = false;
        Cursor.lockState = CursorLockMode.Locked;
    }

    void TampilkanCursor()
    {
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
    }
}
