using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class animationStateController : MonoBehaviour
{
    Animator animator;
    int walkHash;
    int runHash;

    // Start is called before the first frame update
    void Start()
    {
        animator = GetComponent<Animator>();
        walkHash = Animator.StringToHash("isWalking"); 
        runHash = Animator.StringToHash("isRunning"); 

    }

    // Update is called once per frame
    void Update()
{
    bool forwardpresskey = Input.GetKey(KeyCode.W);
    bool runpresskey = Input.GetKey(KeyCode.LeftShift);

    bool shouldWalk = forwardpresskey;
    bool shouldRun = forwardpresskey && runpresskey;

    animator.SetBool(walkHash, shouldWalk);
    animator.SetBool(runHash, shouldRun);
}
}
