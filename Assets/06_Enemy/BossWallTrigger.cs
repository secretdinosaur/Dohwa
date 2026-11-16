using UnityEngine;

public class BossWallTrigger : MonoBehaviour
{
    public EnemyBossLogic bossLogic;

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            if (bossLogic != null && !bossLogic.fightStarted)
            {
                bossLogic.StartBossFight();
                bossLogic.GetComponent<EnemyCore>().CmdForceDetectPlayer?.Invoke();
            }
        }
    }
}
