// using System.Collections.Generic;
// using TMPro;
// using UnityEngine;
// using UnityEngine.EventSystems;
// using UnityEngine.InputSystem;
// using UnityEngine.InputSystem.UI;
//
// namespace SAS.Utilities.DeveloperConsole
// {
//     public class ConsoleCommandMenu : MonoBehaviour
//     {
//         [SerializeField] private ConsoleCommandButton m_ConsoleCommandButton;
//         [SerializeField] private Transform m_Content;
//         private DeveloperConsoleBehaviour _developerConsoleBehaviour;
//         private DeveloperConsole _developerConsole;
//
//         private Queue<ConsoleCommandButton> _consoleCommandsButton = new Queue<ConsoleCommandButton>();
//         private GameObject _eventSystemLastSelectedObject;
//         private InputAction _navigationInputAction;
//         private TMP_InputField m_InputField;
//
//         private void Awake()
//         {
//             _developerConsoleBehaviour = GetComponentInParent<DeveloperConsoleBehaviour>();
//             _developerConsole = _developerConsoleBehaviour.DeveloperConsole;
//             _navigationInputAction = EventSystem.current?.GetComponent<InputSystemUIInputModule>()?.actionsAsset["Navigate"];
//             m_InputField = transform.parent.GetComponentInChildren<TMP_InputField>();
//         }
//
//         private void Update()
//         {
//             if (_navigationInputAction != null)
//             {
//                 var value = _navigationInputAction.ReadValue<Vector2>();
//                 if (value.y < 0)
//                 {
//                     if (EventSystem.current?.currentSelectedGameObject == m_InputField.gameObject)
//                     {
//                         m_InputField.enabled = false;
//                         EventSystem.current.SetSelectedGameObject(null);
//                         EventSystem.current.SetSelectedGameObject(m_Content.GetChild(0).gameObject);
//                         m_InputField.enabled = true;
//                     }
//                 }
//             }
//         }
//
//         private void OnEnable()
//         {
//             foreach (var command in _developerConsole.Commands)
//             {
//                 if(command == null || !(command as ConsoleCommand).UICommand)
//                     continue;
//                 if (!_consoleCommandsButton.TryDequeue(out var commandButton))
//                     commandButton = Instantiate(m_ConsoleCommandButton);
//                 commandButton.transform.SetParent(m_Content, false);
//                 commandButton.transform.SetAsLastSibling();
//                 commandButton.transform.name = command.CommandWord;
//                 commandButton.gameObject.SetActive(true);
//                 commandButton.Init(command, _developerConsoleBehaviour);
//             }
//
//             _eventSystemLastSelectedObject = EventSystem.current.currentSelectedGameObject;
//             EventSystem.current.SetSelectedGameObject(m_Content.GetChild(0).gameObject);
//         }
//
//         void OnDisable()
//         {
//             foreach (Transform transform in m_Content.transform)
//             {
//                 _consoleCommandsButton.Enqueue(transform.GetComponent<ConsoleCommandButton>());
//                 transform.gameObject.SetActive(false);
//             }
//
//             if (EventSystem.current != null)
//                 EventSystem.current.SetSelectedGameObject(_eventSystemLastSelectedObject);
//         }
//     }
// }
