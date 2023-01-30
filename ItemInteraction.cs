using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using System.Linq;

/* GOOD TO KNOW:  Hide/Show is disabling/enabling renderers, lights and colliders (not triggers)  */

public class ItemInteraction : MonoBehaviour
{
    [SerializeField] public actionTypes actionType;
    [SerializeField] public actionTriggers actionTrigger;
    [SerializeField] public KeyCode actionKey = KeyCode.F;
    [SerializeField] public bool repeatable = true;

    [SerializeField] public bool additionalCondition = false;
    [SerializeField] public GameObject conditionObject = null;
    [SerializeField] public AdditionalConditions condition;

    [SerializeField] public bool delegateAction = false;
    [SerializeField] public GameObject delegateActionTo = null;

    private bool interactionDisabled = false;
    private bool isInCollider = false;
    private Animator itemAnimator;
    private AudioSource itemAudioSource;
    private ParticleSystem itemParticles;
    private Dictionary<string, bool> controlledAnimationParameters = new Dictionary<string, bool>();

    private GameObject targetItem = null;

    public enum actionTypes { AnimationTriggers, Show, Hide, AudioPlay, AudioStop, ParticlesPlay, ParticlesStop }
    public enum actionTriggers { Default, OnEnter, OnExit, OnKey }
    public enum AdditionalConditions { IsVisible, IsHidden }

    private actionTypes[] animationGroup = { actionTypes.AnimationTriggers };
    private actionTypes[] audioGroup = { actionTypes.AudioPlay, actionTypes.AudioStop };
    private actionTypes[] particlesGroup = { actionTypes.ParticlesPlay, actionTypes.ParticlesStop };

    void Start()
    {
        targetItem = (delegateAction && delegateActionTo != null) ? delegateActionTo : gameObject;

        if (animationGroup.Contains(actionType))
        {
            if (targetItem.TryGetComponent(out itemAnimator))
            {
                controlledAnimationParameters.Add("enter", false);
                controlledAnimationParameters.Add("exit", false);
                controlledAnimationParameters.Add("key", false);
                foreach (AnimatorControllerParameter p in itemAnimator.parameters)
                {
                    if (controlledAnimationParameters.ContainsKey(p.name))
                        controlledAnimationParameters[p.name] = true;
                }
            }
            else
                disableWithWarning("No Animator in " + targetItem.name + ".");
        }
        else if (audioGroup.Contains(actionType))
        {
            if (!targetItem.TryGetComponent(out itemAudioSource))
                disableWithWarning("No AudioSource in " + targetItem.name + ".");
        }
        else if (particlesGroup.Contains(actionType))
        {
            if (!targetItem.TryGetComponent(out itemParticles))
                disableWithWarning("No ParticleSystem in " + targetItem.name + ".");
        }

        if (actionTrigger == actionTriggers.Default)
            InvokeAction("default");
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        OnEnter();
    }
    private void OnTriggerExit2D(Collider2D other)
    {
        OnExit();
    }

    private void OnTriggerEnter(Collider other)
    {
        OnEnter();
    }

    private void OnTriggerExit(Collider other)
    {
        OnExit();
    }

    private void Update()
    {
        if (interactionDisabled || !isAdditionalConditionPassed())
            return;

        if ((actionTrigger == actionTriggers.OnKey || actionType == actionTypes.AnimationTriggers) && isInCollider && Input.GetKeyDown(actionKey))
            InvokeAction("key");
    }

    private void OnEnter()
    {
        isInCollider = true;
        if (interactionDisabled || !isAdditionalConditionPassed())
            return;

        if (actionTrigger == actionTriggers.OnEnter || actionType == actionTypes.AnimationTriggers)
            InvokeAction("enter");
    }

    private void OnExit()
    {
        isInCollider = false;
        if (interactionDisabled || !isAdditionalConditionPassed())
            return;

        if (actionTrigger == actionTriggers.OnExit || actionType == actionTypes.AnimationTriggers)
            InvokeAction("exit");
    }

    private void InvokeAction(string trigger)
    {
        switch (actionType)
        {
            case actionTypes.AnimationTriggers:
                if (controlledAnimationParameters.ContainsKey(trigger))
                    SetAnimatorTrigger(trigger);
                interactionFinalize();
                break;
            case actionTypes.Show:
                Show();
                interactionFinalize();
                break;
            case actionTypes.Hide:
                Hide();
                interactionFinalize();
                break;
            case actionTypes.AudioPlay:
                PlayAudio();
                interactionFinalize();
                break;
            case actionTypes.AudioStop:
                StopAudio();
                interactionFinalize();
                break;
            case actionTypes.ParticlesPlay:
                PlayParticles();
                interactionFinalize();
                break;
            case actionTypes.ParticlesStop:
                StopParticles();
                interactionFinalize();
                break;
        }
    }

    private void Hide()
    {
        foreach (Renderer r in targetItem.GetComponentsInChildren<Renderer>())
            r.enabled = false;
        foreach (Light l in targetItem.GetComponentsInChildren<Light>())
            l.enabled = false;
        foreach (Collider c in targetItem.GetComponentsInChildren<Collider>())
            c.enabled = (c.isTrigger) ? c.enabled : false;
    }

    private void Show()
    {
        foreach (Renderer r in targetItem.GetComponentsInChildren<Renderer>())
            r.enabled = true;
        foreach (Light l in targetItem.GetComponentsInChildren<Light>())
            l.enabled = true;
        foreach (Collider c in targetItem.GetComponentsInChildren<Collider>())
            c.enabled = (c.isTrigger) ? c.enabled : true;
    }

    private void PlayAudio()
    {
        if (!itemAudioSource.isPlaying)
            itemAudioSource.Play();
    }

    private void StopAudio()
    {
        itemAudioSource.Stop();
    }

    private void PlayParticles()
    {
        if (!itemParticles.isPlaying)
            itemParticles.Play();
    }

    private void StopParticles()
    {
        itemParticles.Stop();
    }

    private void SetAnimatorTrigger(string triggerName)
    {
        if (!controlledAnimationParameters[triggerName])
            return;

        itemAnimator.SetTrigger(triggerName);

        if (!repeatable)
            controlledAnimationParameters[triggerName] = false;
    }

    private void interactionFinalize()
    {
        if (!repeatable && actionType == actionTypes.AnimationTriggers && !controlledAnimationParameters.ContainsValue(true))
            interactionDisabled = true;
            
        else if (!repeatable && actionType != actionTypes.AnimationTriggers)
            interactionDisabled = true;
    }

    private void disableWithWarning(string warning)
    {
        interactionDisabled = true;
        Debug.LogWarning(warning);
    }

    private bool isAdditionalConditionPassed()
    {
        if (!additionalCondition || conditionObject == null)
            return true;

        bool expectVisible = (condition == AdditionalConditions.IsVisible);

        // check if every renderer are desabled (don't check lights and colliders)
        foreach (Renderer r in conditionObject.GetComponentsInChildren<Renderer>())
        {
            if (r.enabled != expectVisible)
                return false;
        }
        return true;
    }
}

#if UNITY_EDITOR
[CustomEditor(typeof(ItemInteraction))]
[CanEditMultipleObjects]
public class ItemInteractionEditor : Editor
{
    SerializedProperty actionType;
    SerializedProperty actionTrigger;
    SerializedProperty actionKey;
    SerializedProperty repeatable;
    SerializedProperty additionalCondition;
    SerializedProperty conditionObject;
    SerializedProperty condition;
    SerializedProperty delegateAction;
    SerializedProperty delegateActionTo;

    void OnEnable()
    {        
        actionType = serializedObject.FindProperty("actionType");
        actionTrigger = serializedObject.FindProperty("actionTrigger");
        actionKey = serializedObject.FindProperty("actionKey");
        repeatable = serializedObject.FindProperty("repeatable");
        additionalCondition = serializedObject.FindProperty("additionalCondition");
        conditionObject = serializedObject.FindProperty("conditionObject");
        condition = serializedObject.FindProperty("condition");
        delegateAction = serializedObject.FindProperty("delegateAction");
        delegateActionTo = serializedObject.FindProperty("delegateActionTo");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        ItemInteraction itemInteractionComponent = (ItemInteraction)target;

        EditorGUILayout.PropertyField(actionType);

        if (itemInteractionComponent.actionType != ItemInteraction.actionTypes.AnimationTriggers)
            EditorGUILayout.PropertyField(actionTrigger);
        else
            EditorGUILayout.LabelField("Now you can use \"enter\", \"exit\" and \"key\" triggers in the Animator. Use as many of them as you need.", EditorStyles.helpBox);

        if (itemInteractionComponent.actionTrigger == ItemInteraction.actionTriggers.OnKey || itemInteractionComponent.actionType == ItemInteraction.actionTypes.AnimationTriggers)
            EditorGUILayout.PropertyField(actionKey);

        if (itemInteractionComponent.actionTrigger != ItemInteraction.actionTriggers.Default)
            EditorGUILayout.PropertyField(repeatable);
        else if (itemInteractionComponent.actionType != ItemInteraction.actionTypes.AnimationTriggers)
            EditorGUILayout.LabelField(" ", "Play action once when scene is starting.", EditorStyles.helpBox);

        EditorGUILayout.Separator();
        EditorGUILayout.LabelField("Advanced:", EditorStyles.boldLabel);

        EditorGUILayout.PropertyField(additionalCondition);
        if (itemInteractionComponent.additionalCondition)
        {
            EditorGUILayout.LabelField("Choose additional condition for the action above:", EditorStyles.helpBox);
            EditorGUILayout.PropertyField(conditionObject);
            EditorGUILayout.PropertyField(condition);
            EditorGUILayout.Separator();
        }

        EditorGUILayout.PropertyField(delegateAction);
        if (itemInteractionComponent.delegateAction)
        {
            EditorGUILayout.LabelField("Trigger action from this object but play on a different object:", EditorStyles.helpBox);
            EditorGUILayout.PropertyField(delegateActionTo);
        }
        
        serializedObject.ApplyModifiedProperties();
    }
}
#endif