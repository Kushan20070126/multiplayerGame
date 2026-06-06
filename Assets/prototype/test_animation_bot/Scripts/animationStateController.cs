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

        
        bool isWalking = animator.GetBool(walkHash);
        bool isRunning = animator.GetBool(runHash);
        bool forwardpresskey = Input.GetKey("w");
        bool runpresskey = Input.GetKey("left shift");


        if(!isWalking && forwardpresskey)
        {
            animator.SetBool(walkHash, true);
        }
         if(isWalking && !forwardpresskey)
        {
            animator.SetBool(walkHash, false);
        }
        if(isRunning && (forwardpresskey && runpresskey) )
        {
            animator.SetBool(runHash, true);
        }
         if(isRunning && (!forwardpresskey || !runpresskey))
        {
            animator.SetBool(runHash, false);
        }

        
    }
}
