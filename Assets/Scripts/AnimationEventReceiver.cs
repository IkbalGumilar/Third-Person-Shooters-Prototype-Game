using UnityEngine;

public class AnimationEventReceiver : MonoBehaviour
{
    public static void EnsureOn(Animator animator)
    {
        if (animator == null || animator.GetComponent<AnimationEventReceiver>() != null)
        {
            return;
        }

        animator.gameObject.AddComponent<AnimationEventReceiver>();
    }

    public void FootL()
    {
    }

    public void FootR()
    {
    }

    public void Hit()
    {
    }

    public void Shoot()
    {
    }

    public void Attack()
    {
    }

    public void Land()
    {
    }

    public void WeaponSwitch()
    {
    }
}
