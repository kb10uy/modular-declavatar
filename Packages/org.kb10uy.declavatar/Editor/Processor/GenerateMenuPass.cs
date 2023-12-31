using UnityEngine;
using VRC.SDK3.Avatars.ScriptableObjects;
using nadena.dev.modular_avatar.core;
using KusakaFactory.Declavatar.Runtime.Data;

namespace KusakaFactory.Declavatar.Processor
{
    internal sealed class GenerateParameterPass : IDeclavatarPass
    {
        public void Execute(DeclavatarContext context)
        {
            if (context.CreateMenuInstaller) context.MenuInstallRoot.AddComponent<ModularAvatarMenuInstaller>();
            context.MenuInstallRoot.AddComponent<ModularAvatarMenuGroup>();

            foreach (var item in context.AvatarDeclaration.MenuItems)
            {
                GameObject menuItem;
                switch (item)
                {
                    case MenuItem.SubMenu submenu:
                        menuItem = GenerateMenuGroupObject(submenu);
                        break;
                    case MenuItem.Button button:
                        menuItem = GenerateMenuButtonObject(button);
                        break;
                    case MenuItem.Toggle toggle:
                        menuItem = GenerateMenuToggleObject(toggle);
                        break;
                    case MenuItem.Radial radial:
                        menuItem = GenerateMenuRadialObject(radial);
                        break;
                    case MenuItem.TwoAxis twoAxis:
                        menuItem = GenerateMenuTwoAxisObject(twoAxis);
                        break;
                    case MenuItem.FourAxis fourAxis:
                        menuItem = GenerateMenuFourAxisObject(fourAxis);
                        break;
                    default:
                        continue;
                }
                menuItem.transform.parent = context.MenuInstallRoot.transform;
            }
        }

        private GameObject GenerateMenuGroupObject(MenuItem.SubMenu submenu)
        {
            var menuGroupRoot = new GameObject(submenu.Name);
            var menuItemComponent = menuGroupRoot.AddComponent<ModularAvatarMenuItem>();
            menuItemComponent.MenuSource = SubmenuSource.Children;

            var control = menuItemComponent.Control;
            control.type = VRCExpressionsMenu.Control.ControlType.SubMenu;
            control.name = submenu.Name;

            foreach (var item in submenu.Items)
            {
                GameObject menuItem;
                switch (item)
                {
                    case MenuItem.SubMenu submenu2:
                        menuItem = GenerateMenuGroupObject(submenu2);
                        break;
                    case MenuItem.Button button:
                        menuItem = GenerateMenuButtonObject(button);
                        break;
                    case MenuItem.Toggle toggle:
                        menuItem = GenerateMenuToggleObject(toggle);
                        break;
                    case MenuItem.Radial radial:
                        menuItem = GenerateMenuRadialObject(radial);
                        break;
                    case MenuItem.TwoAxis twoAxis:
                        menuItem = GenerateMenuTwoAxisObject(twoAxis);
                        break;
                    case MenuItem.FourAxis fourAxis:
                        menuItem = GenerateMenuFourAxisObject(fourAxis);
                        break;
                    default:
                        continue;
                }
                menuItem.transform.parent = menuGroupRoot.gameObject.transform;
            }

            return menuGroupRoot;
        }

        private GameObject GenerateMenuButtonObject(MenuItem.Button button)
        {
            var menuItemObject = new GameObject(button.Name);
            var menuItemComponent = menuItemObject.AddComponent<ModularAvatarMenuItem>();

            var control = menuItemComponent.Control;
            control.type = VRCExpressionsMenu.Control.ControlType.Button;
            control.name = button.Name;
            control.parameter = new VRCExpressionsMenu.Control.Parameter { name = button.Parameter };
            control.value = button.Value.ConvertToVRCParameterValue();

            return menuItemObject;
        }

        private GameObject GenerateMenuToggleObject(MenuItem.Toggle toggle)
        {
            var menuItemObject = new GameObject(toggle.Name);
            var menuItemComponent = menuItemObject.AddComponent<ModularAvatarMenuItem>();

            var control = menuItemComponent.Control;
            control.type = VRCExpressionsMenu.Control.ControlType.Toggle;
            control.name = toggle.Name;
            control.parameter = new VRCExpressionsMenu.Control.Parameter { name = toggle.Parameter };
            control.value = toggle.Value.ConvertToVRCParameterValue();

            return menuItemObject;
        }

        private GameObject GenerateMenuRadialObject(MenuItem.Radial radial)
        {
            var menuItemObject = new GameObject(radial.Name);
            var menuItemComponent = menuItemObject.AddComponent<ModularAvatarMenuItem>();

            var control = menuItemComponent.Control;
            control.type = VRCExpressionsMenu.Control.ControlType.RadialPuppet;
            control.name = radial.Name;
            control.subParameters = new VRCExpressionsMenu.Control.Parameter[]
            {
                new() { name = radial.Parameter },
            };
            control.labels = new VRCExpressionsMenu.Control.Label[]
            {
                new() { name = "Should Insert Label here" },
            };

            return menuItemObject;
        }

        private GameObject GenerateMenuTwoAxisObject(MenuItem.TwoAxis twoAxis)
        {
            var menuItemObject = new GameObject(twoAxis.Name);
            var menuItemComponent = menuItemObject.AddComponent<ModularAvatarMenuItem>();

            var control = menuItemComponent.Control;
            control.type = VRCExpressionsMenu.Control.ControlType.TwoAxisPuppet;
            control.name = twoAxis.Name;
            control.subParameters = new VRCExpressionsMenu.Control.Parameter[]
            {
                new() { name = twoAxis.HorizontalAxis.Parameter },
                new() { name = twoAxis.VerticalAxis.Parameter },
            };
            control.labels = new VRCExpressionsMenu.Control.Label[]
            {
                new() { name = twoAxis.VerticalAxis.LabelPositive },
                new() { name = twoAxis.HorizontalAxis.LabelPositive },
                new() { name = twoAxis.VerticalAxis.LabelNegative },
                new() { name = twoAxis.HorizontalAxis.LabelNegative },
            };

            return menuItemObject;
        }

        private GameObject GenerateMenuFourAxisObject(MenuItem.FourAxis fourAxis)
        {
            var menuItemObject = new GameObject(fourAxis.Name);
            var menuItemComponent = menuItemObject.AddComponent<ModularAvatarMenuItem>();

            var control = menuItemComponent.Control;
            control.type = VRCExpressionsMenu.Control.ControlType.FourAxisPuppet;
            control.name = fourAxis.Name;
            control.subParameters = new VRCExpressionsMenu.Control.Parameter[]
            {
                new() { name = fourAxis.UpAxis.Parameter },
                new() { name = fourAxis.RightAxis.Parameter },
                new() { name = fourAxis.DownAxis.Parameter },
                new() { name = fourAxis.LeftAxis.Parameter },
            };
            control.labels = new VRCExpressionsMenu.Control.Label[]
            {
                new() { name = fourAxis.UpAxis.Label },
                new() { name = fourAxis.RightAxis.Label },
                new() { name = fourAxis.DownAxis.Label },
                new() { name = fourAxis.LeftAxis.Label },
            };

            return menuItemObject;
        }
    }
}
