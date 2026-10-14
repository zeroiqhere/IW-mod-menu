#pragma once
#include <stdint.h>

// Fixture storage and lifecycle stay behind a plain C boundary
extern "C" uint32_t IwFixtureStorageSize();
extern "C" uint32_t IwFixtureStorageAlignment();
extern "C" void* IwFixtureConstruct(void* storage, uint32_t bytes, int loggedOn, uint64_t id);
extern "C" void IwFixtureDestroy(void* object);
extern "C" uint32_t IwFixtureLoggedOnCalls(void* object);
extern "C" uint32_t IwFixtureGetterCalls(void* object);
