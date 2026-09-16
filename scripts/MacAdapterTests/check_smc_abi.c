#include <IOKit/IOKitLib.h>
#include <stddef.h>
#include <stdint.h>
#include <stdio.h>

/* AppleSMC wire layout (not a public SDK sensor API). See docs/MAC-SENSORS.md.
   Compile with each native SDK to check C alignment and IOKit argument widths. */
typedef struct { uint8_t major, minor, build, reserved; uint16_t release; } Version;
typedef struct { uint16_t version, length; uint32_t cpu, gpu, memory; } Limits;
typedef struct { uint32_t size, type; uint8_t attributes; } Info;
typedef struct {
    uint32_t key;
    Version version;
    Limits limits;
    Info info;
    uint8_t result, status, command;
    uint32_t index;
    uint8_t bytes[32];
} Request;
_Static_assert(sizeof(io_connect_t)==4, "IOKit connection width");
_Static_assert(sizeof(size_t)==8, "IOKit buffer length width");
_Static_assert(sizeof(Request)==80, "SMC request size");
_Static_assert(offsetof(Request,info)==28, "SMC key metadata offset");
_Static_assert(offsetof(Request,result)==40, "SMC firmware result offset");
_Static_assert(offsetof(Request,command)==42, "SMC command offset");
_Static_assert(offsetof(Request,index)==44, "SMC index offset");
_Static_assert(offsetof(Request,bytes)==48, "SMC payload offset");
int main(void){puts("PASS AppleSMC wire layout on native compiler; firmware compatibility requires live readings");return 0;}
