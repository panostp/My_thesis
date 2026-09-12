#include "register_types.h"
#include "helloWorld.h"
#include "CatmullRom.h"
#include "pathFinding.h"
#include <gdextension_interface.h>
#include <godot_cpp/core/class_db.hpp>
#include <godot_cpp/core/defs.hpp>
#include <godot_cpp/godot.hpp>

using namespace godot;

void register_world_types(ModuleInitializationLevel p_level) 
{
    if(p_level != MODULE_INITIALIZATION_LEVEL_SCENE) {
        return;
    }
    ClassDB::register_class<HelloWorld>();
    ClassDB::register_class<CatmullRom>();
    ClassDB::register_class<PathFinding>();

}
void unregister_world_types(ModuleInitializationLevel p_level) 
{
    if(p_level != MODULE_INITIALIZATION_LEVEL_SCENE) {
        return;
    }    
}


extern "C"
{
	// Initialization
	GDExtensionBool GDE_EXPORT helloWorld_init(GDExtensionInterfaceGetProcAddress p_get_proc_address, GDExtensionClassLibraryPtr p_library, GDExtensionInitialization *r_initialization)
	{
		GDExtensionBinding::InitObject init_obj(p_get_proc_address, p_library, r_initialization);
		init_obj.register_initializer(register_world_types);
		init_obj.register_terminator(unregister_world_types);
		init_obj.set_minimum_library_initialization_level(MODULE_INITIALIZATION_LEVEL_SCENE);

		return init_obj.init();
	}

}
