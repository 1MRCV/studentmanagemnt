pipeline {
    agent { label 'windows-agent' }

    parameters {
        string(
            name: 'BRANCH',
            defaultValue: 'main',
            description: 'Git branch to build'
        )
    }

    environment {

        GIT_REPO = "https://github.com/1MRCV/studentmanagemnt.git"

        PUBLISH_FOLDER = "publish"
        ARTIFACT_NAME = "website_${BUILD_NUMBER}.zip"

        IIS_PATH = "C:\\inetpub\\wwwroot\\testWebsite"
        APP_POOL = "testAppPool"
    }

    stages {

        stage('Clean Workspace') {
            steps {
                deleteDir()
            }
        }

        stage('Checkout Code') {
            steps {
                git branch: "${params.BRANCH}",
                    url: "${env.GIT_REPO}"
            }
        }

        stage('Restore Dependencies') {
            steps {
                powershell '''
                dotnet restore
                '''
            }
        }

        stage('Build Application') {
            steps {
                powershell '''
                dotnet build --configuration Release
                '''
            }
        }

        stage('Publish Website') {
            steps {
                powershell '''
                dotnet publish -c Release -o $env:PUBLISH_FOLDER
                '''
            }
        }

        stage('Create Artifact') {
            steps {
                powershell '''
                Write-Host "Creating deployment package..."

                Compress-Archive `
                    -Path "$env:PUBLISH_FOLDER\\*" `
                    -DestinationPath "$env:ARTIFACT_NAME"

                Write-Host "Artifact created: $env:ARTIFACT_NAME"
                '''
            }
        }

        stage('Deploy to IIS') {
            steps {
                powershell '''
                $ErrorActionPreference = "Stop"

                $zipPath = "$env:WORKSPACE\\$env:ARTIFACT_NAME"
                $deployPath = "$env:IIS_PATH"

                Import-Module WebAdministration

                Write-Host "Checking App Pool state..."
                $state = (Get-WebAppPoolState -Name $env:APP_POOL).Value

                if ($state -eq "Started") {
                    Write-Host "Stopping IIS App Pool..."
                    Stop-WebAppPool -Name $env:APP_POOL
                    Start-Sleep -Seconds 3
                } else {
                    Write-Host "App Pool already stopped."
                }

                Write-Host "Cleaning IIS folder..."
                if (Test-Path $deployPath) {
                    Remove-Item "$deployPath\\*" -Recurse -Force -ErrorAction SilentlyContinue
                }

                Write-Host "Extracting build..."
                Expand-Archive -Path $zipPath -DestinationPath $deployPath -Force

                Write-Host "Starting IIS App Pool..."
                Start-WebAppPool -Name $env:APP_POOL

                Write-Host "Deployment completed successfully."
                '''
            }
        }

        stage('Verify Deployment') {
            steps {
                powershell '''
                Import-Module WebAdministration

                $state = (Get-WebAppPoolState -Name $env:APP_POOL).Value

                Write-Host "App Pool State: $state"

                if ($state -ne "Started") {
                    throw "Deployment verification failed!"
                }

                Write-Host "Deployment verified successfully."
                '''
            }
        }
    }

    post {

        success {
            echo "Build ${BUILD_NUMBER} deployed successfully."
        }

        failure {
            echo "Deployment failed. Please check logs."
        }
    }
}
