pipeline {
    agent { label 'windows-agent' }

    parameters {

        string(
            name: 'BRANCH',
            defaultValue: 'main',
            description: 'Git branch to build'
        )

        string(
            name: 'DEPLOY_BUILD',
            defaultValue: '',
            description: 'Build number to deploy (leave empty for latest)'
        )
    }

    environment {

        GIT_REPO = "https://github.com/1MRCV/studentmanagemnt.git"

        PUBLISH_FOLDER = "publish"

        IIS_PATH = "C:\\inetpub\\wwwroot\\testWebsite"
        APP_POOL = "testAppPool"

        WEBSITE_NAME = "testWebsite"
        WEBSITE_PORT = "8080"

        ARTIFACT_STORAGE = "C:\\jenkins-artifacts"
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

        stage('Create Artifact Version') {
            steps {
                powershell '''

                $storage = "$env:ARTIFACT_STORAGE"
                $zip = "$storage\\build_$env:BUILD_NUMBER.zip"

                if (!(Test-Path $storage)) {
                    New-Item -ItemType Directory -Path $storage | Out-Null
                }

                Write-Host "Creating artifact version $env:BUILD_NUMBER"

                Compress-Archive `
                    -Path "$env:PUBLISH_FOLDER\\*" `
                    -DestinationPath $zip `
                    -Force

                Write-Host "Stored artifact: $zip"

                '''
            }
        }

        stage('Deploy to IIS') {
            steps {
                powershell '''

                $ErrorActionPreference = "Stop"

                Import-Module WebAdministration

                $deployPath = "$env:IIS_PATH"
                $buildToDeploy = "$env:DEPLOY_BUILD"

                if ([string]::IsNullOrEmpty($buildToDeploy)) {

                    Write-Host "Deploying latest build: $env:BUILD_NUMBER"
                    $zipPath = "$env:ARTIFACT_STORAGE\\build_$env:BUILD_NUMBER.zip"
                }
                else {

                    Write-Host "Deploying selected build: $buildToDeploy"
                    $zipPath = "$env:ARTIFACT_STORAGE\\build_$buildToDeploy.zip"
                }

                if (!(Test-Path $zipPath)) {
                    throw "Artifact not found: $zipPath"
                }

                Write-Host "Checking Application Pool..."

                if (!(Test-Path "IIS:\\AppPools\\$env:APP_POOL")) {

                    Write-Host "Creating Application Pool..."
                    New-WebAppPool -Name $env:APP_POOL
                }
                else {
                    Write-Host "Application Pool already exists"
                }

                Write-Host "Checking IIS Website..."

                if (!(Test-Path "IIS:\\Sites\\$env:WEBSITE_NAME")) {

                    Write-Host "Creating IIS Website..."

                    if (!(Test-Path $deployPath)) {
                        New-Item -ItemType Directory -Path $deployPath | Out-Null
                    }

                    New-Website `
                        -Name $env:WEBSITE_NAME `
                        -Port $env:WEBSITE_PORT `
                        -PhysicalPath $deployPath `
                        -ApplicationPool $env:APP_POOL

                    Write-Host "Website created successfully."
                }
                else {
                    Write-Host "Website already exists."
                }

                Write-Host "Checking App Pool state..."

                $state = (Get-WebAppPoolState -Name $env:APP_POOL).Value

                if ($state -eq "Started") {

                    Write-Host "Stopping IIS App Pool..."
                    Stop-WebAppPool -Name $env:APP_POOL
                    Start-Sleep -Seconds 3
                }
                else {

                    Write-Host "App Pool already stopped."
                }

                Write-Host "Preparing IIS directory..."

                if (!(Test-Path $deployPath)) {
                    New-Item -ItemType Directory -Path $deployPath | Out-Null
                }

                Write-Host "Cleaning old files..."
                Remove-Item "$deployPath\\*" -Recurse -Force -ErrorAction SilentlyContinue

                Write-Host "Extracting build..."
                Expand-Archive -Path $zipPath -DestinationPath $deployPath -Force

                Write-Host "Starting IIS App Pool..."
                Start-WebAppPool -Name $env:APP_POOL

                Write-Host "Starting IIS Website..."
                Start-Website -Name $env:WEBSITE_NAME

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
