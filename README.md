# BNM-Il2CppSDKGenerator


SDK Generator for BNM using DummyDll's from Il2CppDumper


## Example code
```cpp
// CUnityEngine to not get in teh way of BNM's UnityEngine setups. Not too big of a deal
#include <CUnityEngine/Object.h>
#include <CUnityEngine/GameObject.h>
#include <CUnityEngine/Transform.h>
#include <CUnityEngine/PrimitiveType.h>
#include <CUnityEngine/Collider.h>
#include <CUnityEngine/Rigidbody.h>
#include <CUnityEngine/Component.h>
#include <BNM/UnityStructures.hpp>
#include <BNM/BasicMonoStructures.hpp>

void Something() {
    using namespace CUnityEngine;
    using namespace BNM::Structures;
    auto obj = GameObject::CreatePrimitive(PrimitiveType::Cube);
    obj->get_transform()->set_localScale(Unity::Vector3::one * 2.0f);
    obj->set_name(BNM::CreateMonoString("Name"));

    GameObject* player = GameObject::Find("Player");
    auto rb = player->GetComponent<Rigidbody*>(); // working-ish generics!!!
    rb->set_useGravity(false);
}
```

## Credits

[BNM-Android](https://github.com/ByNameModding/BNM-Android/) - BNM

[Il2Cpp-Modding-Codegen](https://github.com/sc2ad/Il2Cpp-Modding-Codegen/) - Inspiration, and some ideas