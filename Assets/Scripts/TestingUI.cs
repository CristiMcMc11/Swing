using UnityEngine;
using UnityEngine.UIElements;
using TMPro;
using System.Collections;
using System;

public class TestingUI : MonoBehaviour
{
    public enum Variable
    {
        JumpTime,
        JumpHeight,
        TermVelocity,
        AccelTime,
        AirRes,
        GroundFriction,
        InstantAccel,
        MinSlideSpeed,
        MaxSlideSpeed,
        SlideTweenTime,
        WallJumpVeloX,
        WallJumpVeloY,
        MouseMode,
        GameSpeed,
        WalkSpeed
    }
    private Variable varToChange;
    private PlayerMovement pm;
    private GrapplerMovement gm;

    private void Awake()
    {
        pm = GameObject.Find("Player").GetComponent<PlayerMovement>();
        gm = GameObject.Find("Player").GetComponent<GrapplerMovement>();
    }


    public void ShowMenu()
    {
        gameObject.SetActive(!gameObject.activeSelf);
    }

    public void VariableChanged(string val)
    {
        print(varToChange);
        switch (varToChange)
        {
            case Variable.JumpTime:
                if (int.TryParse(val, out _))
                {
                    pm.maxJumpTime = float.Parse(val);
                }
                break;

            case Variable.JumpHeight:
                if (int.TryParse(val, out _))
                {
                    pm.maxJumpHeight = float.Parse(val);
                }   
                break;

            case Variable.TermVelocity:
                if (int.TryParse(val, out _))
                {
                    pm.terminalVelocity = float.Parse(val);
                }   
                break;

            case Variable.AccelTime:
                if (int.TryParse(val, out _))
                {
                    pm.accelerationTime = float.Parse(val)/1000;
                }  
                break;

            case Variable.GroundFriction:
                if (int.TryParse(val, out _))
                {
                    pm.groundFriction = float.Parse(val);
                }    
                break;

            

            case Variable.MinSlideSpeed:
                if (int.TryParse(val, out _))
                {
                    pm.minSlideSpeed = float.Parse(val);
                }
                break;

            case Variable.MaxSlideSpeed:
                if (int.TryParse(val, out _))
                {
                    pm.maxSlideSpeed = float.Parse(val);
                }
                break;

            case Variable.SlideTweenTime:
                if (int.TryParse(val, out _))
                {
                    pm.slideSpeedTweenTime = float.Parse(val)/1000;
                }
                break;

            case Variable.WallJumpVeloX:
                if (int.TryParse(val, out _))
                {
                    pm.wallJumpVelocity.x = float.Parse(val);
                }
                break;

            case Variable.WallJumpVeloY:
                if (int.TryParse(val, out _))
                {
                    pm.wallJumpVelocity.y = float.Parse(val);
                }
                break;

            case Variable.GameSpeed:
                if (int.TryParse(val, out _))
                {
                    pm.gameSpeed = float.Parse(val)/100;
                }
                break;

            case Variable.WalkSpeed:
                if (int.TryParse(val, out _))
                {
                    pm.walkSpeed = float.Parse(val);
                }
                break;
        }
    }

    public void SetDefault()
    {
        switch (varToChange)
        {
            case Variable.JumpTime:
                pm.maxJumpTime = 1;
                break;

            case Variable.JumpHeight:
                pm.maxJumpHeight = 4;
                break;

            case Variable.TermVelocity:
                pm.terminalVelocity = -25;
                break;

            case Variable.AccelTime:
                pm.accelerationTime = 0.1f;
                break;

            case Variable.AirRes:
                pm.airResistance = 0.1f;
                break;

            case Variable.GroundFriction:
                pm.airResistance = 50;
                break;

            case Variable.InstantAccel:
                pm.instantAccelerate = false;
                break;

            case Variable.MinSlideSpeed:
                pm.minSlideSpeed = 0.1f;
                break;

            case Variable.MaxSlideSpeed:
                pm.maxSlideSpeed = 4;
                break;

            case Variable.SlideTweenTime:
                pm.slideSpeedTweenTime = 0.2f;
                break;

            case Variable.WallJumpVeloX:
                pm.wallJumpVelocity.x = 13;
                break;

            case Variable.WallJumpVeloY:
                pm.wallJumpVelocity.y = 10;
                break;

            case Variable.MouseMode:
                gm.mouseMode = true;
                break;

            case Variable.GameSpeed:
                pm.gameSpeed = 1;
                break;

            case Variable.WalkSpeed:
                pm.walkSpeed = 8;
                break;
        }
    }

    public void BoolChanged(bool val)
    {
        switch (varToChange)
        {
            case Variable.InstantAccel:
                pm.instantAccelerate = val;
                break;

            case Variable.MouseMode:
                gm.mouseMode = val;
                break;
        }
    }

    public void SetVarToChange(int index)
    {
        varToChange = (Variable)index;
    }
}
