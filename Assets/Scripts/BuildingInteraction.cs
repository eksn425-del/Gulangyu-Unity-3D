using UnityEngine;

public class BuildingInteraction : MonoBehaviour
{
    private void OnCollisionEnter(Collision collision)
    {
        // 当碰撞物体的 Tag 为 'Player' 时
        if (collision.gameObject.CompareTag("Player"))
        {
            // 打印日志：'[系统日志] 视障用户已成功触发建筑感官交互，ID 为: ' + gameObject.name
            Debug.Log("[系统日志] 视障用户已成功触发建筑感官交互，ID 为: " + gameObject.name);
        }
    }
}
