#include<gdextension_interface.h>
#include <godot_cpp/core/class_db.hpp>
#include <godot_cpp/core/defs.hpp>
#include <godot_cpp/godot.hpp>
#include <godot_cpp/classes/node.hpp>

using namespace godot;

class MyClass : public Node {
    GDCLASS(MyClass, Node);

protected:
    static void _bind_methods() {
        ClassDB::bind_method(D_METHOD("say_hello"), &MyClass::say_hello);
    }

public:
    void say_hello() {
        UtilityFunctions::print("Hello from test2!");
    }
};

// Register the class with Godot

