using System;
using System.Collections;
using ActionRPG.Core;
using Unity.Netcode;
using UnityEngine;

namespace ActionRPG.Tutorial
{

    /// <summary>
    /// 튜토리얼 진행자. 스텝을 순서대로 돌리고 목표 문구/대사를 이벤트로 흘려보낸다.
    ///
    /// 튜토리얼도 실제 게임과 같은 프리팹·시스템을 쓰기 위해 호스트 모드로 혼자 돈다.
    /// 그래야 여기서 배운 조작이 멀티에서 그대로 통한다.
    /// </summary>
    [DisallowMultipleComponent]
    public class TutorialDirector : MonoBehaviour
    {
        [SerializeField] private TutorialSequence sequence;
        [Tooltip("튜토리얼 씬에서 자동으로 호스트를 시작할지. 메뉴에서 이미 시작했다면 끈다.")]
        [SerializeField] private bool autoStartHost = true;
        [Tooltip("스텝에서 이름으로 찾을 수 있는 씬 마커들.")]
        [SerializeField] private Transform[] markers;
        [Tooltip("적을 소환할 때 기준이 되는 지면 레이어.")]
        [SerializeField] private LayerMask groundMask = 1;

        private TutorialStep[] _runtimeSteps;
        private TutorialContext _context;
        private int _index = -1;
        private bool _running;
        private float _stepElapsed;

        /// <summary>(index, total, step)</summary>
        public event Action<int, int, TutorialStep> StepStarted;
        public event Action Finished;

        public TutorialStep CurrentStep => _index >= 0 && _runtimeSteps != null && _index < _runtimeSteps.Length ? _runtimeSteps[_index] : null;
        public int CurrentIndex => _index;
        public int StepCount => _runtimeSteps?.Length ?? 0;

        private IEnumerator Start()
        {
            if (sequence == null || sequence.Steps.Length == 0)
            {
                Debug.LogWarning("[TutorialDirector] 시퀀스가 비어 있습니다.");
                yield break;
            }

            if (autoStartHost) yield return StartCoroutine(EnsureHost());

            // 플레이어 캐릭터가 스폰될 때까지 기다린다.
            float timeout = Time.time + 15f;
            while (ActorRegistry.LocalPlayer == null && Time.time < timeout) yield return null;

            if (ActorRegistry.LocalPlayer == null)
            {
                Debug.LogError("[TutorialDirector] 로컬 플레이어를 찾지 못했습니다. NetworkManager 의 Player Prefab 설정을 확인하세요.");
                yield break;
            }

            BuildRuntimeSteps();

            _context = new TutorialContext { Director = this, Player = ActorRegistry.LocalPlayer };
            _running = true;

            if (!string.IsNullOrEmpty(sequence.Intro)) GameEventBus.RaiseDialogue(sequence.Intro, 5f);
            Advance();
        }

        private IEnumerator EnsureHost()
        {
            var nm = NetworkManager.Singleton;
            if (nm == null)
            {
                Debug.LogError("[TutorialDirector] 씬에 NetworkManager 가 없습니다.");
                yield break;
            }

            if (nm.IsListening) yield break;

            nm.NetworkConfig.ConnectionApproval = false;
            nm.StartHost();
            yield return null;
        }

        /// <summary>에셋을 그대로 쓰면 진행 상태가 프로젝트 파일에 남으므로 복제해서 돌린다.</summary>
        private void BuildRuntimeSteps()
        {
            _runtimeSteps = new TutorialStep[sequence.Steps.Length];
            for (int i = 0; i < sequence.Steps.Length; i++)
            {
                var source = sequence.Steps[i];
                _runtimeSteps[i] = source != null ? Instantiate(source) : null;
            }
        }

        private void Update()
        {
            if (!_running) return;

            var step = CurrentStep;
            if (step == null) return;

            float dt = Time.deltaTime;
            _stepElapsed += dt;
            _context.ElapsedInStep = _stepElapsed;

            bool complete = step.Tick(_context, dt);
            if (!complete && step.TimeoutSeconds > 0f && _stepElapsed >= step.TimeoutSeconds) complete = true;
            if (!complete) return;

            StartCoroutine(CompleteCurrentStep(step));
        }

        private IEnumerator CompleteCurrentStep(TutorialStep step)
        {
            _running = false;
            if (step.CompletionDelay > 0f) yield return new WaitForSeconds(step.CompletionDelay);

            step.Exit(_context);
            Advance();
        }

        private void Advance()
        {
            _index++;

            if (_runtimeSteps == null || _index >= _runtimeSteps.Length)
            {
                _running = false;
                GameEventBus.RaiseObjectiveTextChanged(string.Empty);
                Finished?.Invoke();
                return;
            }

            var step = _runtimeSteps[_index];
            if (step == null) { Advance(); return; }

            _stepElapsed = 0f;
            _running = true;

            step.Enter(_context);
            GameEventBus.RaiseObjectiveTextChanged(step.ObjectiveText);
            if (!string.IsNullOrEmpty(step.DialogueLine)) GameEventBus.RaiseDialogue(step.DialogueLine, step.DialogueDuration);

            StepStarted?.Invoke(_index, _runtimeSteps.Length, step);
        }

        /// <summary>스텝을 건너뛴다. 디버그/테스터용.</summary>
        public void SkipCurrentStep()
        {
            var step = CurrentStep;
            if (step == null) return;
            step.Exit(_context);
            Advance();
        }

        public Transform FindMarker(string markerName)
        {
            if (markers == null || string.IsNullOrEmpty(markerName)) return null;
            for (int i = 0; i < markers.Length; i++)
                if (markers[i] != null && markers[i].name == markerName) return markers[i];
            return null;
        }

        /// <summary>튜토리얼용 적 소환. 호스트이므로 서버 권한으로 바로 스폰한다.</summary>
        public void SpawnTutorialEnemies(GameObject prefab, int count, float distance)
        {
            var nm = NetworkManager.Singleton;
            if (nm == null || !nm.IsServer || prefab == null) return;

            var player = ActorRegistry.LocalPlayer;
            Vector3 center = player != null ? player.transform.position : transform.position;

            for (int i = 0; i < count; i++)
            {
                float angle = 360f / Mathf.Max(1, count) * i;
                Vector3 offset = Quaternion.Euler(0f, angle, 0f) * Vector3.forward * distance;
                Vector3 position = center + offset;

                if (Physics.Raycast(position + Vector3.up * 5f, Vector3.down, out var hit, 20f, groundMask, QueryTriggerInteraction.Ignore))
                    position = hit.point;

                var instance = Instantiate(prefab, position, Quaternion.LookRotation(center - position, Vector3.up));
                var netObject = instance.GetComponent<NetworkObject>();
                if (netObject == null)
                {
                    Debug.LogError($"[TutorialDirector] {prefab.name} 에 NetworkObject 가 없습니다.", prefab);
                    Destroy(instance);
                    continue;
                }
                netObject.Spawn(true);
            }
        }
    }
}
