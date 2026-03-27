using UnityEngine;
using UnityEngine.AI;

public class PlayerController : MonoBehaviour
{
    // 注意：请在 Unity 的 Inspector 面板里：
    // 1. 将 Rigidbody 的 Drag 设为 1。
    // 2. 在 Constraints 中勾选 Freeze Rotation 的 X、Y、Z 轴。

    public float speed = 7.0f;
    public bool autoNavMode = false;

    private Rigidbody rb;
    private NavMeshAgent navAgent;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        navAgent = GetComponent<NavMeshAgent>();

        if (navAgent != null)
        {
            navAgent.updateRotation = true;
            navAgent.updatePosition = true;
        }
    }

    void FixedUpdate()
    {
        if (rb != null)
        {
            rb.freezeRotation = true;
        }

        if (autoNavMode)
        {
            return;
        }

        // 获取输入：W/S 控制前后 (Vertical)，A/D 控制左右平移 (Horizontal)
        float moveX = Input.GetAxis("Horizontal");
        float moveZ = Input.GetAxis("Vertical");

        // 计算移动方向（相对于玩家自身的正前方和正右方）
        Vector3 moveDirection = (transform.forward * moveZ + transform.right * moveX).normalized;

        // 使用 Rigidbody 控制速度来实现平滑移动，同时保留重力影响 (rb.velocity.y)
        // 这样物理碰撞会比 transform.Translate 更真实
        Vector3 targetVelocity = moveDirection * speed;
        
        // 我们只改变水平方向的速度，保持垂直方向（重力）的速度不变
        rb.velocity = new Vector3(targetVelocity.x, rb.velocity.y, targetVelocity.z);
    }

    public void SetDestination(Vector3 target)
    {
        NavMeshAgent agent = GetComponent<NavMeshAgent>();
        if (agent == null)
        {
            return;
        }

        agent.SetDestination(target);
    }
}
