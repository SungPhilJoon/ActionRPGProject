using Framework;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// 액터 이동. 플레이어는 CharacterController(직접 조작), 몬스터는 NavMeshAgent(경로 탐색)를 쓴다.
/// 어빌리티는 어느 쪽이든 같은 함수(Face / LockMovement / Dash)만 호출한다 (Adapter 역할).
/// 이동 속도는 GAS 속성 MoveSpeed 를 매 프레임 읽으므로 슬로우/헤이스트 이펙트가 바로 반영된다.
/// </summary>
public class ActorMotor : MonoBehaviour
{
    private const float Gravity = -20.0f;
    private const float TurnSpeed = 720.0f;
    /// <summary>몬스터 회전 부드러움 (클수록 빠르게 따라감). 10 이면 약 0.2초에 86% 회전.</summary>
    private const float AgentTurnSharpness = 10.0f;
    private const int AvoidancePriorityBase = 30;
    private const int AvoidancePriorityRange = 40;
    /// <summary>이 거리보다 가까운 경로 모서리는 바라보기 기준으로 쓰지 않는다 (도착 직전 방향 튐 방지).</summary>
    private const float SteeringMinDistance = 0.3f;

    private Actor Owner;
    private CharacterController Controller;
    private NavMeshAgent Agent;

    private Vector3 DesiredDirection;
    private float MoveLockRemaining;
    private Vector3 DashVelocity;
    private float DashRemaining;
    private float VerticalVelocity;
    private Quaternion TargetRotation;
    private bool HasTargetRotation;

    public float CurrentSpeed { get; private set; }

    public bool IsDashing
    {
        get { return DashRemaining > 0.0f; }
    }

    public bool IsMovementLocked
    {
        get { return MoveLockRemaining > 0.0f || IsDashing == true || isStunned() == true; }
    }

    public bool UsesAgent
    {
        get { return Agent != null; }
    }

    /// <summary>경로 계산 중 (긴 경로는 여러 프레임 걸린다). 이때 SetDestination 을 다시 부르면 계산이 처음부터 다시 시작된다.</summary>
    public bool IsPathPending
    {
        get { return Agent != null && Agent.isOnNavMesh == true && Agent.pathPending == true; }
    }

    /// <summary>따라갈 경로가 있거나 계산 중인가.</summary>
    public bool HasPathOrPending
    {
        get { return Agent != null && Agent.isOnNavMesh == true && (Agent.hasPath == true || Agent.pathPending == true); }
    }

    public void Initialize(Actor InOwner)
    {
        Owner = InOwner;
        Controller = GetComponent<CharacterController>();
        Agent = GetComponent<NavMeshAgent>();
        if (Agent != null)
        {
            Agent.updateRotation = false;
            Agent.acceleration = 40.0f;
            Agent.angularSpeed = 0.0f;
            // 움직이는 대상을 쫓으므로 목적지 근처 자동 감속을 끈다 (도착 판정은 ChaseTargetTask 가 사거리로 한다).
            Agent.autoBraking = false;
            Agent.obstacleAvoidanceType = ObstacleAvoidanceType.MedQualityObstacleAvoidance;
            // 회피 우선순위가 모두 같으면 서로 양보하다 좌우로 떨린다 → 개체마다 다르게.
            Agent.avoidancePriority = AvoidancePriorityBase + (GetInstanceID() & 0x7FFFFFFF) % AvoidancePriorityRange;
        }
    }

    /// <summary>플레이어 입력 방향 (월드, 평면).</summary>
    public void SetMoveInput(Vector3 InWorldDirection)
    {
        DesiredDirection = Vector3.ClampMagnitude(MathUtil.Flat(InWorldDirection), 1.0f);
    }

    public void SetDestination(Vector3 InDestination)
    {
        if (Agent == null || Agent.isOnNavMesh == false)
        {
            return;
        }

        Agent.isStopped = false;
        Agent.SetDestination(InDestination);
    }

    public void Stop()
    {
        DesiredDirection = Vector3.zero;
        if (Agent != null && Agent.isOnNavMesh == true)
        {
            Agent.isStopped = true;
            Agent.ResetPath();
        }
    }

    public void Face(Vector3 InDirection, bool InInstant)
    {
        InDirection.y = 0.0f;
        if (InDirection.sqrMagnitude < 0.0001f)
        {
            return;
        }

        TargetRotation = Quaternion.LookRotation(InDirection.normalized, Vector3.up);
        HasTargetRotation = true;
        if (InInstant == true)
        {
            transform.rotation = TargetRotation;
        }
    }

    public void LockMovement(float InSeconds)
    {
        MoveLockRemaining = Mathf.Max(MoveLockRemaining, InSeconds);
        if (Agent != null && Agent.isOnNavMesh == true)
        {
            Agent.isStopped = true;
        }
    }

    public void Dash(Vector3 InDirection, float InDistance, float InDuration)
    {
        InDirection = MathUtil.Flat(InDirection).normalized;
        float duration = Mathf.Max(0.05f, InDuration);
        DashVelocity = InDirection * (InDistance / duration);
        DashRemaining = duration;
        Face(InDirection, true);
    }

    public void CancelDash()
    {
        DashRemaining = 0.0f;
    }

    public void Teleport(Vector3 InPosition)
    {
        if (Controller != null)
        {
            Controller.enabled = false;
            transform.position = InPosition;
            Controller.enabled = true;
        }
        else if (Agent != null)
        {
            Agent.Warp(InPosition);
        }
        else
        {
            transform.position = InPosition;
        }

        VerticalVelocity = 0.0f;
    }

    private void Update()
    {
        if (Owner == null)
        {
            return;
        }

        float deltaTime = Time.deltaTime;
        if (MoveLockRemaining > 0.0f)
        {
            MoveLockRemaining -= deltaTime;
        }

        if (Owner.IsDead == true)
        {
            CurrentSpeed = 0.0f;
            if (Agent != null && Agent.isOnNavMesh == true)
            {
                Agent.isStopped = true;
            }

            return;
        }

        float moveSpeed = Owner.Attributes.Get(EAttribute.MoveSpeed);
        if (Controller != null)
        {
            updateController(deltaTime, moveSpeed);
        }
        else if (Agent != null)
        {
            updateAgent(deltaTime, moveSpeed);
        }

        if (HasTargetRotation == true)
        {
            if (Agent != null)
            {
                // 몬스터: 목적지 갱신(0.25초)·경로 모서리 변경 때 방향이 한 프레임에 8~10° 튀던 것을 지수 감쇠로 여러 프레임에 나눈다.
                float blend = 1.0f - Mathf.Exp(-AgentTurnSharpness * deltaTime);
                transform.rotation = Quaternion.Slerp(transform.rotation, TargetRotation, blend);
            }
            else
            {
                transform.rotation = Quaternion.RotateTowards(transform.rotation, TargetRotation, TurnSpeed * deltaTime);
            }
        }
    }

    private void updateController(float InDeltaTime, float InMoveSpeed)
    {
        Vector3 velocity;
        if (IsDashing == true)
        {
            DashRemaining -= InDeltaTime;
            velocity = DashVelocity;
        }
        else if (IsMovementLocked == true)
        {
            velocity = Vector3.zero;
        }
        else
        {
            velocity = DesiredDirection * InMoveSpeed;
            if (DesiredDirection.sqrMagnitude > 0.01f && Owner.Asc.Tags.HasTag((int)ETag.State_Attacking) == false)
            {
                Face(DesiredDirection, false);
            }
        }

        if (Controller.isGrounded == true && VerticalVelocity < 0.0f)
        {
            VerticalVelocity = -2.0f;
        }

        VerticalVelocity += Gravity * InDeltaTime;
        velocity.y = VerticalVelocity;
        Controller.Move(velocity * InDeltaTime);

        CurrentSpeed = MathUtil.Flat(velocity).magnitude;
    }

    private void updateAgent(float InDeltaTime, float InMoveSpeed)
    {
        if (Agent.isOnNavMesh == false)
        {
            CurrentSpeed = 0.0f;
            return;
        }

        Agent.speed = InMoveSpeed;
        if (IsDashing == true)
        {
            DashRemaining -= InDeltaTime;
            Agent.Move(DashVelocity * InDeltaTime);
            CurrentSpeed = DashVelocity.magnitude;
            return;
        }

        if (IsMovementLocked == true)
        {
            Agent.isStopped = true;
            CurrentSpeed = 0.0f;
            return;
        }

        Vector3 velocity = Agent.velocity;
        CurrentSpeed = MathUtil.Flat(velocity).magnitude;
        if (CurrentSpeed > 0.1f)
        {
            // 회피(RVO)가 섞인 velocity 는 프레임마다 방향이 흔들린다 → 경로의 다음 모서리(steeringTarget)를 바라본다.
            Vector3 toSteering = MathUtil.Flat(Agent.steeringTarget - transform.position);
            Face(Agent.hasPath == true && toSteering.magnitude > SteeringMinDistance ? toSteering : velocity, false);
        }
    }

    private bool isStunned()
    {
        return Owner != null && Owner.Asc != null && (Owner.Asc.Tags.HasTag((int)ETag.State_Debuff_Stun) == true || Owner.Asc.Tags.HasTag((int)ETag.State_Feeding) == true);
    }
}
