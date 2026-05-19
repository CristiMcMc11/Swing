using UnityEngine;
using static PlayerMovement;

public class PlayerVisuals : MonoBehaviour
{
    private PlayerMovement pmScript;
    private GrapplerMovement gmScript;
    private Animator animator;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Awake()
    {
        pmScript = transform.parent.GetComponent<PlayerMovement>(); 
        gmScript = transform.parent.GetComponent<GrapplerMovement>();
        animator = GetComponent<Animator>();
    }

    // Update is called once per frame
    void LateUpdate()
    {
        SetAnimatorParameters();
    }

    private void SetAnimatorParameters()
    {
        animator.SetInteger("PlayerState", ((int)pmScript.playerState));
        animator.SetBool("Moving", pmScript.GetVelocity().x != 0);
        animator.SetBool("Falling", pmScript.GetVelocity().y < 0);
    }
}
