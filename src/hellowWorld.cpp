#include "helloWorld.h"
#include <godot_cpp/core/class_db.hpp>
#include <godot_cpp/godot.hpp>
#include <ostream>
#include <godot_cpp/variant/utility_functions.hpp>

using namespace godot;

void HelloWorld::_bind_methods() 
{
    ClassDB::bind_method(D_METHOD("hello", "word"), &HelloWorld::hello);
    //ClassDB::bind_method(D_METHOD("_process", "delta"), &HelloWorld::_process);
}
HelloWorld::HelloWorld() 
{
    UtilityFunctions::print("hello world from C++");
}
HelloWorld::~HelloWorld() 
{
    
}
void HelloWorld::hello(String word) 
{
    message = "Hello, " + word + "!";
    UtilityFunctions::print(message);
}
void HelloWorld::_process(double delta) 
{
    // You can add code here that needs to be executed every frame
} 