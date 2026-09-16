using System.Collections.Generic;
using UnityEngine;

public static class ObjectHeadAreaHealing
{
    public static int Apply(Vector2 point,float radius,int amount)
    {
        if(radius<=0 || amount<=0) return 0;
        var treated=new HashSet<CharacterCombat>();
        int total=0;
        foreach(var hit in Physics2D.OverlapCircleAll(point,radius))
        {
            var combat=hit.GetComponentInParent<CharacterCombat>();
            // Deliberately no owner, team or alliance filter: green flares heal enemies too.
            if(combat!=null && treated.Add(combat)) total+=combat.Heal(amount);
        }
        return total;
    }
}
