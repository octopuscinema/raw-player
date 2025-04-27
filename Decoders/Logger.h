#pragma once

#include <string>

namespace Octopus::Player::Core
{
    typedef void (*LogFunction)(const char* message);

    class Logger
    {
    public:

        Logger(LogFunction logFunction)
            : m_LogFunction(logFunction)
        {

        }

        void Log(const std::string& message) const
        {
            m_LogFunction(message.c_str());
        }

    private:

        LogFunction m_LogFunction;
    };
}