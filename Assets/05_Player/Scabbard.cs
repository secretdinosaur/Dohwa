using UnityEngine;

public class Scabbard : MonoBehaviour
{
    public GameObject Sword;
    public GameObject Hand;
    public GameObject SwordCase;
    public bool IsDrawn = false;
    private bool lastDrawn;

    public Vector3 drawnPosition = new Vector3(-0.0003f, 0.0118f, -0.0005f);
    public Vector3 drawnRotation = new Vector3(0, -90, 0);

    public Vector3 sheathedPosition = new Vector3(-0.0004f, 0.0185f, 0.0005f);
    public Vector3 sheathedRotation = new Vector3(180, -90, 2.4f);

    void Start()
    {
        Sword.transform.localPosition = sheathedPosition;
        Sword.transform.localRotation = Quaternion.Euler(sheathedRotation);
        Sheath();
    }

    public void Draw()
    {
        Sword.transform.SetParent(Hand.transform);
        Sword.transform.localPosition = drawnPosition;
        Sword.transform.localRotation = Quaternion.Euler(drawnRotation);
        IsDrawn = true;
    }

    public void Sheath()
    {
        Sword.transform.SetParent(SwordCase.transform);
        Sword.transform.localPosition = sheathedPosition;
        Sword.transform.localRotation = Quaternion.Euler(sheathedRotation);
        IsDrawn = false;
    }

    private void LateUpdate()
    {
        if (!IsDrawn)
        {
            Sheath();
        }

        if (!Player.Instance.PlayerAttack.isSwordMoving || (lastDrawn != IsDrawn))
        {
            if (IsDrawn)
            {
                Sword.transform.localPosition = drawnPosition;
                Sword.transform.localRotation = Quaternion.Euler(drawnRotation);
            }
            else
            {
                Sword.transform.localPosition = sheathedPosition;
                Sword.transform.localRotation = Quaternion.Euler(sheathedRotation);
            }
        }
        lastDrawn = IsDrawn;
    }
}