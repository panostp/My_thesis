#ifndef HELLOWORLD_H
#define HELLOWORLD_H

#include <godot_cpp/classes/node2d.hpp>

namespace godot 
{

    class HelloWorld : public Node2D 
    {
        GDCLASS(HelloWorld, Node2D);

        private:
            String message;
        protected:
            static void _bind_methods();
        public:
            HelloWorld();
            ~HelloWorld();
            void hello(String word);
            void _process(double delta);
    };
}
#endif // HELLOWORLD_H