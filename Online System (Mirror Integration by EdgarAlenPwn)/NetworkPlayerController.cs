using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Mirror;
using System.Linq;

public class NetworkPlayerController : NetworkBehaviour
{
    [Header("Components")]
    public GameObject GameManagerChild;
    public Camera playerCamera;
    public Animator animator;
    public TextMesh healthBar;

    public playerWeaponSystem currentWeapon;
    public NetworkAnimator networkAnimator2;
    [HideInInspector]
    public GameObject projectileToFire;

    private void Awake()
    {
        GameManagerChild.transform.SetParent(null);
        playerCamera.enabled = false;
    }

    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {

    }


}
