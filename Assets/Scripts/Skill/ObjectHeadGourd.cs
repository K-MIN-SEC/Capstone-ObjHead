using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>Per-character finite catalogue. Random outcomes are chosen by simulation authority at fire time.</summary>
public sealed class ObjectHeadGourd : MonoBehaviour
{
    private readonly Dictionary<string,int> charges=new Dictionary<string,int>();
    private List<ObjectHeadHeadChoice> catalogue;
    private System.Random random;
    public string SelectedId {get;private set;}
    public string LastResolvedId {get;private set;}
    public ObjectHeadHeadChoice SelectedChoice=>Choices.FirstOrDefault(c=>c.id==SelectedId);
    public string ConfirmedId {get;set;}
    public static bool IsMeta(SkillEffectType type)=>type==SkillEffectType.GourdRandom||type==SkillEffectType.GourdRefill||type==SkillEffectType.GourdSelect;
    public List<ObjectHeadHeadChoice> Choices
    {
        get
        {
            if(catalogue==null)
            {
                var language=(ObjectHeadLanguage)PlayerPrefs.GetInt("ObjectHead.Language",0);
                catalogue=ObjectHeadHeadChoice.Catalog(ObjectHeadContent.Load(),Resources.Load<ObjectHeadLocalizationTable>("ObjectHeadLocalization"),language)
                    .Where(c=>c.source==null || !IsMeta(c.Resolve().effectType)).ToList();
                foreach(var c in catalogue)charges[c.id]=1;
                var member=GetComponent<ObjectHeadTeamMember>();
                random=new System.Random((GameStartData.Instance?.mapSeed??0)^(member?.PlayerIndex??1)*397^(member?.TeamSlotIndex??0)*7919);
            }
            return catalogue;
        }
    }
    public int Remaining(ObjectHeadHeadChoice c){_ = Choices;return ObjectHeadTraining.Enabled?-1:charges.TryGetValue(c.id,out int n)?n:0;}
    public bool Select(string id)
    {
        var choice=Choices.FirstOrDefault(c=>c.id==id);if(choice==null || Remaining(choice)==0)return false;
        SelectedId=id;return true;
    }
    public void Refill(){foreach(var c in Choices)charges[c.id]=Math.Min(999,charges[c.id]+1);}
    public ObjectHeadGourdCharge[] Capture()=>Choices.Select(c=>new ObjectHeadGourdCharge{id=c.id,count=charges[c.id]}).ToArray();
    public void Apply(ObjectHeadGourdCharge[] state)
    {
        if(state==null)return;_ = Choices;
        foreach(var item in state)if(item!=null && charges.ContainsKey(item.id))charges[item.id]=Mathf.Clamp(item.count,0,999);
    }
    public bool Fire(SkillEffectType type,float power,bool publish)
    {
        var turn=FindAnyObjectByType<TurnManager>();var actor=GetComponent<TurnCharacterController>();var selector=GetComponent<DemoSkillSelector>();
        if(turn==null || !selector.CanUseSelectedSkill())return false;
        bool authority=ObjectHeadCommonAuthority.CanWrite;
        if(type==SkillEffectType.GourdRefill)
        {
            if(!(publish?turn.TryBeginAction(actor):turn.TryBeginReplicatedAction(actor)))return false;
            if(authority)Refill();selector.NotifySkillFired();
            ObjectHeadActionCues.Refill(transform);
            GetComponent<SkillFireController>().PublishSpecialFire(power,publish);
            turn.NotifyActionResolved();return true;
        }
        var choice=authority
            ? type==SkillEffectType.GourdRandom ? Choices[random.Next(Choices.Count)] : Choices.FirstOrDefault(c=>c.id==SelectedId)
            : Choices.FirstOrDefault(c=>c.id==ConfirmedId);
        if(choice==null || (authority && type==SkillEffectType.GourdSelect && Remaining(choice)==0))return false;
        // Resolved identity is not set by aiming, inspecting the inventory or charging.
        LastResolvedId=choice.id;
        bool fired;
        if(choice.common!=CommonHeadType.None)
        {
            fired=GetComponent<CommonHeadUseController>().UseBorrowedHead(choice.common,power,publish);
            if(fired){selector.NotifySkillFired();GetComponent<SkillFireController>().PublishSpecialFire(power,publish);}
        }
        else fired=GetComponent<SkillFireController>().FireBorrowed(choice.Resolve(),power,publish);
        if(!fired){LastResolvedId=null;return false;}
        ObjectHeadActionCues.Reveal(transform,choice.icon);
        if(authority && type==SkillEffectType.GourdSelect && !ObjectHeadTraining.Enabled)charges[choice.id]--;
        return true;
    }
}

[Serializable] public sealed class ObjectHeadGourdCharge {public string id;public int count;}
