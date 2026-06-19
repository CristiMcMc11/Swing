using UnityEngine;
using UnityEngine.UIElements;
using TMPro;
using System.Collections;
using System;
using System.Collections.Generic;
using UnityEngine.UI;
using Unity.VisualScripting;

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
        WalkSpeed,
        GrappleVelMult,
        GrapplePullSpeed
    }
    private Variable varToChange;
    private PlayerMovement pm;
    private GrapplerMovement gm;

    private List<object> defaults = null;
    private List<Transform> inputs = new List<Transform>();

    private void Awake()
    {
        pm = GameObject.Find("Player").GetComponent<PlayerMovement>();
        gm = GameObject.Find("Player").GetComponent<GrapplerMovement>();
    }

    private void Start()
    {
        SetDefaultsList();
        SetInputsList();
        SetDefaultTexts();
    }

    private void SetDefaultsList()
    {
        defaults = new List<object> { pm.maxJumpTime, pm.maxJumpHeight, pm.terminalVelocity, pm.accelerationTime, pm.airResistance, pm.groundFriction,
            pm.instantAccelerate, pm.minSlideSpeed, pm.maxSlideSpeed, pm.slideSpeedTweenTime, pm.wallJumpVelocity.x, pm.wallJumpVelocity.y, gm.mouseMode, pm.gameSpeed,
            pm.walkSpeed, gm.grappleVelocityMultiplier, gm.pullSpeed};
    }

    private void SetInputsList()
    {
        Transform inputsParent = transform.Find("Viewport").Find("Content").Find("Inputs");
        foreach (Transform child in inputsParent)
        {
            inputs.Add(child);
        }
    }

    private void SetDefaultTexts()
    {
        Transform inputsParent = transform.Find("Viewport").Find("Content").Find("Inputs");
        List<TMP_InputField> texts = new List<TMP_InputField>();

        foreach (Transform child in inputsParent)
        {
            if (child.Find("InputField") != null)
            {
                texts.Add(child.Find("InputField").GetComponent<TMP_InputField>());
            }
            else
            {
                texts.Add(null);
            }
        }

        for (int i = 0; i < texts.Count; i++)
        {
            if (defaults[i].GetType() == typeof(float))
            {
                texts[i].text = defaults[i].ToString();
            }
        }    
    }

    private void SetDefaultUI(Variable var)
    {
        int i = (int)var;        

        if (defaults[i].GetType() == typeof(float))
        {
            inputs[i].Find("InputField").GetComponent<TMP_InputField>().text = defaults[i].ToString();
        }
        else if (defaults[i].GetType() == typeof(bool)) {
            inputs[i].Find("Toggle").GetComponent<UnityEngine.UI.Toggle>().isOn = (bool)defaults[i];
        }
    }

    public void ShowMenu()
    {
        gameObject.SetActive(!gameObject.activeSelf);
    }

    public void VariableChanged(string val)
    {
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

            case Variable.GrappleVelMult:
                if (int.TryParse(val, out _))
                {
                    gm.grappleVelocityMultiplier = float.Parse(val);
                }
                break;

            case Variable.GrapplePullSpeed:
                if (int.TryParse(val, out _))
                {
                    gm.pullSpeed = float.Parse(val);
                }
                break;
        }
    }

    public void SetDefault()
    {
        switch (varToChange)
        {
            case Variable.JumpTime:
                pm.maxJumpTime = (float)defaults[0];
                break;

            case Variable.JumpHeight:
                pm.maxJumpHeight = (float)defaults[1];
                break;

            case Variable.TermVelocity:
                pm.terminalVelocity = (float)defaults[2];
                break;

            case Variable.AccelTime:
                pm.accelerationTime = (float)defaults[3];
                break;

            case Variable.AirRes:
                pm.airResistance = (float)defaults[4];
                break;

            case Variable.GroundFriction:
                pm.airResistance = (float)defaults[5];
                break;

            case Variable.InstantAccel:
                pm.instantAccelerate = (bool)defaults[6];
                break;

            case Variable.MinSlideSpeed:
                pm.minSlideSpeed = (float)defaults[7];
                break;

            case Variable.MaxSlideSpeed:
                pm.maxSlideSpeed = (float)defaults[8];
                break;

            case Variable.SlideTweenTime:
                pm.slideSpeedTweenTime = (float)defaults[9];
                break;

            case Variable.WallJumpVeloX:
                pm.wallJumpVelocity.x = (float)defaults[10];
                break;

            case Variable.WallJumpVeloY:
                pm.wallJumpVelocity.y = (float)defaults[11];
                break;

            case Variable.MouseMode:
                gm.mouseMode = (bool)defaults[12];
                break;

            case Variable.GameSpeed:
                pm.gameSpeed = (float)defaults[13];
                break;

            case Variable.WalkSpeed:
                pm.walkSpeed = (float)defaults[14];
                break;

            case Variable.GrappleVelMult:
                gm.grappleVelocityMultiplier = (float)defaults[15];
                break;

            case Variable.GrapplePullSpeed:
                gm.pullSpeed = (float)defaults[16];
                break;

        }

        SetDefaultUI(varToChange);
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
